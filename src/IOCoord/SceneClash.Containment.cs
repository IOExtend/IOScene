using IOScene;

namespace IOCoord;

public static partial class SceneClash
{
    /// <summary>Finds placed solids fully enclosed by another placed solid without surface contact.</summary>
    /// <param name="scene">The scene whose placed triangle meshes are tested.</param>
    /// <param name="inner">Ids of elements that may be contained.</param>
    /// <param name="outer">Ids of elements that may contain them.</param>
    /// <param name="cancellationToken">Cancellation token for the query.</param>
    /// <returns>One result per element pair with at least one contained instance. The contained id comes first.</returns>
    /// <remarks>Triangle meshes must describe closed, consistently oriented boundaries without self-intersections.
    /// Opposite orientation distinguishes cavities from exterior shells. Lines and points are ignored.</remarks>
    /// <exception cref="InvalidDataException">A selected triangle mesh has a degenerate face or an unpaired or inconsistently oriented edge.</exception>
    public static SceneClashTest FindSolidContainments(SceneDocument scene, IEnumerable<long> inner, IEnumerable<long> outer,
        CancellationToken cancellationToken = default) =>
        Find(scene, inner, outer, Query.Containment, 0, cancellationToken);

    private static bool StrictlyWithin(BoundingBox3d inner, BoundingBox3d outer) =>
        inner.Min.X > outer.Min.X && inner.Min.Y > outer.Min.Y && inner.Min.Z > outer.Min.Z
        && inner.Max.X < outer.Max.X && inner.Max.Y < outer.Max.Y && inner.Max.Z < outer.Max.Z;

    private static ClosedSolid BuildClosedSolid(SceneGeometry geometry, Transform3d transform, long elementId,
        CancellationToken cancellationToken)
    {
        var vertices = new Vector3d[geometry.Vertices.Count];
        var vertexIds = new int[vertices.Length];
        var welded = new Dictionary<Vector3d, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            vertices[i] = PlaceVertex(geometry.Vertices[i], transform);
            welded.TryAdd(vertices[i], i);
            vertexIds[i] = welded[vertices[i]];
        }

        bool indexed = geometry.Indices.Count != 0;
        int count = (indexed ? geometry.Indices.Count : vertices.Length) / 3;
        if (count == 0) throw InvalidMesh();
        var triangles = new Primitive[count];
        var components = new int[count];
        var edges = new Dictionary<(int First, int Second), (int Face, bool Forward, bool Paired)>();
        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int offset = i * 3;
            int ai = indexed ? geometry.Indices[offset] : offset;
            int bi = indexed ? geometry.Indices[offset + 1] : offset + 1;
            int ci = indexed ? geometry.Indices[offset + 2] : offset + 2;
            var a = vertices[ai];
            var b = vertices[bi];
            var c = vertices[ci];
            var normal = Vector3d.Cross(b - a, c - a);
            double length = SceneMeasure.Length(normal);
            if (length == 0) throw InvalidMesh();
            triangles[i] = new(elementId, BoundingBox3d.Empty.Encapsulate(a).Encapsulate(b).Encapsulate(c),
                a, b, c, normal / length);
            components[i] = i;
            AddEdge(vertexIds[ai], vertexIds[bi], i);
            AddEdge(vertexIds[bi], vertexIds[ci], i);
            AddEdge(vertexIds[ci], vertexIds[ai], i);
        }
        if (edges.Values.Any(edge => !edge.Paired)) throw InvalidMesh();

        var boundaryPoints = new List<Vector3d>();
        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (components[i] == i) boundaryPoints.Add(triangles[i].A);
        }
        return new(triangles, Build(triangles, 0, triangles.Length, cancellationToken), boundaryPoints);

        void AddEdge(int from, int to, int face)
        {
            bool forward = from < to;
            var key = forward ? (from, to) : (to, from);
            if (!edges.TryGetValue(key, out var other))
            {
                edges.Add(key, (face, forward, false));
                return;
            }
            if (other.Paired || other.Forward == forward) throw InvalidMesh();
            edges[key] = (other.Face, other.Forward, true);
            components[Root(face)] = Root(other.Face);
        }

        int Root(int face)
        {
            while (components[face] != face)
            {
                components[face] = components[components[face]];
                face = components[face];
            }
            return face;
        }

        InvalidDataException InvalidMesh() =>
            new($"Geometry {geometry.Id} of element {elementId} must have nondegenerate faces and consistently oriented paired edges.");
    }

    private static bool IsContained(ClosedSolid inner, ClosedSolid outer, CancellationToken token)
    {
        if (SurfacesTouch(inner, outer, inner.Tree, outer.Tree, token)) return false;
        // Disjoint boundaries lie on one side per connected component. Test both boundaries to account for cavities.
        return inner.BoundaryPoints.All(point => IsInside(outer, point, token))
            && !outer.BoundaryPoints.Any(point => IsInside(inner, point, token));
    }

    private static bool SurfacesTouch(ClosedSolid a, ClosedSolid b, Node one, Node two, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Overlaps(one.Bounds, two.Bounds, Tolerance)) return false;
        if (one.First is not null && (two.First is null || one.Count >= two.Count))
            return SurfacesTouch(a, b, one.First, two, token) || SurfacesTouch(a, b, one.Second!, two, token);
        if (two.First is not null)
            return SurfacesTouch(a, b, one, two.First, token) || SurfacesTouch(a, b, one, two.Second!, token);
        for (int i = one.Start; i < one.Start + one.Count; i++)
        for (int j = two.Start; j < two.Start + two.Count; j++)
        {
            var x = a.Triangles[i];
            var y = b.Triangles[j];
            if (Overlaps(x.Bounds, y.Bounds, Tolerance) && Contact(x, y) is not null) return true;
        }
        return false;
    }

    private static bool IsInside(ClosedSolid solid, Vector3d point, CancellationToken token)
    {
        if (!StrictlyWithin(new(point, point), solid.Tree.Bounds)) return false;
        double angle = 0, compensation = 0;
        foreach (var triangle in solid.Triangles)
        {
            token.ThrowIfCancellationRequested();
            var a = Unit(triangle.A - point);
            var b = Unit(triangle.B - point);
            var c = Unit(triangle.C - point);
            // Van Oosterom and Strackee's oriented solid angle, using unit vectors from the query point.
            double numerator = Vector3d.Dot(a, Vector3d.Cross(b, c));
            double denominator = 1 + Vector3d.Dot(a, b) + Vector3d.Dot(b, c) + Vector3d.Dot(c, a);
            double term = 2 * Math.Atan2(numerator, denominator) - compensation;
            double sum = angle + term;
            compensation = (sum - angle) - term;
            angle = sum;
        }
        return Math.Abs(angle) > 2 * Math.PI;

        static Vector3d Unit(Vector3d vector) => vector / SceneMeasure.Length(vector);
    }

    private sealed record ClosedSolid(Primitive[] Triangles, Node Tree, List<Vector3d> BoundaryPoints);
}
