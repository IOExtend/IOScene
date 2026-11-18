using IOScene;

namespace IOCoord;

/// <summary>Finds surface intersections between the triangle meshes of scene elements.</summary>
public static partial class SceneClash
{
    // The same rounding tolerance is used for the bounds, plane distances and edge distances.
    private const double Tolerance = 1e-9;
    private const int LeafSize = 8;

    /// <summary>Finds one contact point per intersecting pair from the two supplied element selections.</summary>
    /// <param name="scene">The scene whose placed triangles are tested.</param>
    /// <param name="first">Ids of elements in the first selection.</param>
    /// <param name="second">Ids of elements in the second selection.</param>
    /// <param name="cancellationToken">Cancellation token for the query.</param>
    /// <returns>A clash test with the intersections. The input scene is unchanged.</returns>
    /// <remarks>Tests triangle surfaces in metres. A contact tolerance of one nanometre covers rounding.
    /// Penetration depth, clearance and solids enclosed without surface contact are outside this query.</remarks>
    public static SceneClashTest FindSurfaceIntersections(SceneDocument scene, IEnumerable<long> first, IEnumerable<long> second,
        CancellationToken cancellationToken = default) =>
        Find(scene, first, second, null, cancellationToken);

    private static SceneClashTest Find(SceneDocument scene, IEnumerable<long> first, IEnumerable<long> second,
        double? maxClearance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        cancellationToken.ThrowIfCancellationRequested();
        if (scene.Validate().FirstOrDefault(issue => issue.Severity == SceneSeverity.Error) is { } error)
            throw new InvalidDataException($"Invalid scene ({error.Code}): {error.Message}");

        var left = first.ToHashSet();
        var right = second.ToHashSet();
        var elements = scene.Elements.ToDictionary(element => element.Id);
        if (left.Concat(right).Any(id => !elements.ContainsKey(id)))
            throw new ArgumentException("The selections must contain ids of elements in the scene.");

        var geometries = scene.Geometries.ToDictionary(geometry => geometry.Id);
        var leftTriangles = new List<Triangle>();
        var rightTriangles = new List<Triangle>();
        foreach (var instance in scene.Instances)
        {
            bool inLeft = left.Contains(instance.ElementId), inRight = right.Contains(instance.ElementId);
            if (!inLeft && !inRight) continue;
            var geometry = geometries[instance.GeometryId];
            if (geometry.Topology != SceneTopology.Triangles) continue;
            var transform = instance.Transform ?? Transform3d.Identity;
            var indices = geometry.Indices;
            bool indexed = indices.Count > 0;
            int count = indexed ? indices.Count : geometry.Vertices.Count;
            for (int offset = 0; offset < count; offset += 3)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var a = WorldVertex(offset);
                var b = WorldVertex(offset + 1);
                var c = WorldVertex(offset + 2);
                var faceNormal = Vector3d.Cross(b - a, c - a);
                double length = faceNormal.Length;
                if (!double.IsFinite(length)) throw new InvalidDataException("A triangle normal is outside the finite range.");
                if (length == 0) continue;
                var normal = faceNormal / length;
                var bounds = BoundingBox3d.Empty.Encapsulate(a).Encapsulate(b).Encapsulate(c);
                var triangle = new Triangle(instance.ElementId, a, b, c, normal, bounds);
                if (inLeft) leftTriangles.Add(triangle);
                if (inRight) rightTriangles.Add(triangle);
            }

            Vector3d WorldVertex(int index)
            {
                var point = transform.Apply(geometry.Vertices[indexed ? indices[index] : index]);
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
                    throw new InvalidDataException("The transformed coordinates are outside the finite range.");
                return point;
            }
        }

        var test = new SceneClashTest { Name = maxClearance is null ? "Surface intersections" : "Surface clearances" };
        if (leftTriangles.Count == 0 || rightTriangles.Count == 0) return test;
        Triangle[] aTriangles = leftTriangles.ToArray(), bTriangles = rightTriangles.ToArray();
        var aRoot = Build(aTriangles, 0, aTriangles.Length, cancellationToken);
        var bRoot = Build(bTriangles, 0, bTriangles.Length, cancellationToken);
        double margin = Math.Max(Tolerance, maxClearance ?? 0);
        var contacts = new Dictionary<(long First, long Second), (Vector3d Point, double? Distance)>();
        var pending = new Stack<(Node First, Node Second)>();
        pending.Push((aRoot, bRoot));
        while (pending.TryPop(out var pair))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (a, b) = pair;
            if (!Overlaps(a.Bounds, b.Bounds, margin)) continue;
            if (a.First is not null && (b.First is null || a.Count >= b.Count))
            {
                pending.Push((a.First, b));
                pending.Push((a.Second!, b));
            }
            else if (b.First is not null)
            {
                pending.Push((a, b.First));
                pending.Push((a, b.Second!));
            }
            else
            {
                for (int i = a.Start; i < a.Start + a.Count; i++)
                {
                    var one = aTriangles[i];
                    for (int j = b.Start; j < b.Start + b.Count; j++)
                    {
                        var two = bTriangles[j];
                        if (one.ElementId == two.ElementId || !Overlaps(one.Bounds, two.Bounds, margin)) continue;
                        var key = (Math.Min(one.ElementId, two.ElementId), Math.Max(one.ElementId, two.ElementId));
                        bool found = contacts.TryGetValue(key, out var best);
                        if (found && (maxClearance is null || best.Distance == 0)) continue;
                        if (best.Distance is double distance && !Overlaps(one.Bounds, two.Bounds, Math.Max(Tolerance, distance))) continue;

                        if ((maxClearance is null || Overlaps(one.Bounds, two.Bounds, Tolerance))
                            && Contact(one, two) is { } point)
                        {
                            contacts[key] = (point, maxClearance is null ? null : 0);
                        }
                        else if (maxClearance is double limit)
                        {
                            var clearance = ClosestBetween(one, two);
                            if (clearance.Distance <= limit && (!found || clearance.Distance < best.Distance))
                                contacts[key] = clearance;
                        }
                    }
                }
            }
        }

        foreach (var (key, contact) in contacts.OrderBy(pair => pair.Key))
        {
            string firstName = elements[key.First].Name;
            string secondName = elements[key.Second].Name;
            test.Clashes.Add(new SceneClashResult
            {
                Id = FormattableString.Invariant($"{key.First}:{key.Second}"),
                Name = $"{firstName} / {secondName}",
                Element1Id = key.First,
                Element2Id = key.Second,
                Element1Name = firstName,
                Element2Name = secondName,
                Point = contact.Point,
                Distance = contact.Distance,
                Status = SceneClashStatus.New,
            });
        }
        return test;
    }

    private static Node Build(Triangle[] triangles, int start, int count, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var bounds = BoundingBox3d.Empty;
        for (int i = start; i < start + count; i++) bounds = bounds.Encapsulate(triangles[i].Bounds);
        if (count <= LeafSize) return new(bounds, start, count);
        Vector3d extent = bounds.Size;
        var comparer = extent.X >= extent.Y && extent.X >= extent.Z ? ByX : extent.Y >= extent.Z ? ByY : ByZ;
        Array.Sort(triangles, start, count, comparer);
        int half = count / 2;
        var left = Build(triangles, start, half, token);
        var right = Build(triangles, start + half, count - half, token);
        return new(bounds, start, count, left, right);
    }

    private static bool Overlaps(BoundingBox3d a, BoundingBox3d b, double margin) =>
        a.Min.X <= b.Max.X + margin && b.Min.X <= a.Max.X + margin
        && a.Min.Y <= b.Max.Y + margin && b.Min.Y <= a.Max.Y + margin
        && a.Min.Z <= b.Max.Z + margin && b.Min.Z <= a.Max.Z + margin;

    private static Vector3d? Contact(Triangle a, Triangle b) =>
        EdgeContact(a.A, a.B, b) ?? EdgeContact(a.B, a.C, b) ?? EdgeContact(a.C, a.A, b)
        ?? EdgeContact(b.A, b.B, a) ?? EdgeContact(b.B, b.C, a) ?? EdgeContact(b.C, b.A, a);

    private static Vector3d? EdgeContact(Vector3d from, Vector3d to, Triangle triangle)
    {
        double start = Vector3d.Dot(from - triangle.A, triangle.Normal);
        double end = Vector3d.Dot(to - triangle.A, triangle.Normal);
        bool startOnPlane = Math.Abs(start) <= Tolerance, endOnPlane = Math.Abs(end) <= Tolerance;
        if (startOnPlane && Inside(from, triangle)) return from;
        if (endOnPlane && Inside(to, triangle)) return to;
        if (startOnPlane && endOnPlane)
        {
            return CoplanarCrossing(from, to, triangle.A, triangle.B, triangle.Normal)
                ?? CoplanarCrossing(from, to, triangle.B, triangle.C, triangle.Normal)
                ?? CoplanarCrossing(from, to, triangle.C, triangle.A, triangle.Normal);
        }
        if ((start < 0) == (end < 0)) return null;
        Vector3d point = from + (to - from) * (start / (start - end));
        return Inside(point, triangle) ? point : null;
    }

    private static bool Inside(Vector3d point, Triangle triangle)
    {
        return HasLeftSide(triangle.A, triangle.B) && HasLeftSide(triangle.B, triangle.C) && HasLeftSide(triangle.C, triangle.A);

        bool HasLeftSide(Vector3d from, Vector3d to)
        {
            var edge = to - from;
            return Vector3d.Dot(Vector3d.Cross(edge, point - from), triangle.Normal) >= -Tolerance * edge.Length;
        }
    }

    private static Vector3d? CoplanarCrossing(Vector3d a, Vector3d b, Vector3d c, Vector3d d, Vector3d normal)
    {
        var u = b - a;
        var v = d - c;
        var delta = c - a;
        double denominator = Vector3d.Dot(Vector3d.Cross(u, v), normal);
        if (denominator == 0) return null;
        double t = Vector3d.Dot(Vector3d.Cross(delta, v), normal) / denominator;
        double s = Vector3d.Dot(Vector3d.Cross(delta, u), normal) / denominator;
        return t is >= 0 and <= 1 && s is >= 0 and <= 1 ? a + u * t : null;
    }

    private static readonly Comparer<Triangle> ByX = Comparer<Triangle>.Create((a, b) => a.Bounds.Center.X.CompareTo(b.Bounds.Center.X));
    private static readonly Comparer<Triangle> ByY = Comparer<Triangle>.Create((a, b) => a.Bounds.Center.Y.CompareTo(b.Bounds.Center.Y));
    private static readonly Comparer<Triangle> ByZ = Comparer<Triangle>.Create((a, b) => a.Bounds.Center.Z.CompareTo(b.Bounds.Center.Z));

    private readonly record struct Triangle(long ElementId, Vector3d A, Vector3d B, Vector3d C, Vector3d Normal, BoundingBox3d Bounds);
    private sealed record Node(BoundingBox3d Bounds, int Start, int Count, Node? First = null, Node? Second = null);
}
