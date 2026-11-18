using IOScene;

namespace IOCoord;

/// <summary>Checks placed scene geometry for intersections, clearances and overlap.</summary>
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
        Find(scene, first, second, Query.Intersection, 0, cancellationToken);

    private static SceneClashTest Find(SceneDocument scene, IEnumerable<long> first, IEnumerable<long> second,
        Query query, double limit, CancellationToken cancellationToken)
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
        var leftPrimitives = new List<Primitive>();
        var rightPrimitives = new List<Primitive>();
        var meshBounds = query == Query.Hard ? new List<BoundingBox3d>() : null;
        for (int placement = 0; placement < scene.Instances.Count; placement++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var instance = scene.Instances[placement];
            bool inLeft = left.Contains(instance.ElementId), inRight = right.Contains(instance.ElementId);
            if (!inLeft && !inRight) continue;
            var geometry = geometries[instance.GeometryId];
            if (query is not (Query.Overlap or Query.Duplicate) && geometry.Topology != SceneTopology.Triangles) continue;
            var transform = instance.Transform ?? Transform3d.Identity;

            int meshIndex = -1;
            if (query is Query.Overlap or Query.Hard or Query.Duplicate)
            {
                var bounds = BoundingBox3d.Empty;
                foreach (var vertex in geometry.Vertices)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    bounds = bounds.Encapsulate(WorldVertex(vertex));
                }
                if (bounds.IsEmpty) continue;
                if (meshBounds is null)
                {
                    Add(new(instance.ElementId, bounds, MeshIndex: placement));
                    continue;
                }
                meshIndex = meshBounds.Count;
                meshBounds.Add(bounds);
            }

            var indices = geometry.Indices;
            bool indexed = indices.Count > 0;
            int count = indexed ? indices.Count : geometry.Vertices.Count;
            for (int offset = 0; offset < count; offset += 3)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var a = WorldVertex(geometry.Vertices[indexed ? indices[offset] : offset]);
                var b = WorldVertex(geometry.Vertices[indexed ? indices[offset + 1] : offset + 1]);
                var c = WorldVertex(geometry.Vertices[indexed ? indices[offset + 2] : offset + 2]);
                var faceNormal = Vector3d.Cross(b - a, c - a);
                double length = faceNormal.Length;
                if (!double.IsFinite(length)) throw new InvalidDataException("A triangle normal is outside the finite range.");
                if (length == 0) continue;
                var bounds = BoundingBox3d.Empty.Encapsulate(a).Encapsulate(b).Encapsulate(c);
                Add(new(instance.ElementId, bounds, a, b, c, faceNormal / length, meshIndex));
            }

            void Add(Primitive primitive)
            {
                if (inLeft) leftPrimitives.Add(primitive);
                if (inRight) rightPrimitives.Add(primitive);
            }

            Vector3d WorldVertex(Vector3d vertex)
            {
                var point = transform.Apply(vertex);
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
                    throw new InvalidDataException("The transformed coordinates are outside the finite range.");
                return point;
            }
        }

        var test = new SceneClashTest
        {
            Name = query switch
            {
                Query.Clearance => "Surface clearances",
                Query.Hard => "Hard clashes",
                Query.Overlap => "Bounding box overlaps",
                Query.Duplicate => "Duplicate geometry",
                _ => "Surface intersections",
            },
        };
        if (leftPrimitives.Count == 0 || rightPrimitives.Count == 0) return test;
        Primitive[] aPrimitives = leftPrimitives.ToArray(), bPrimitives = rightPrimitives.ToArray();
        var aRoot = Build(aPrimitives, 0, aPrimitives.Length, cancellationToken);
        var bRoot = Build(bPrimitives, 0, bPrimitives.Length, cancellationToken);
        double margin = query switch
        {
            Query.Overlap => 0,
            Query.Clearance => Math.Max(Tolerance, limit),
            Query.Duplicate => limit,
            _ => Tolerance,
        };
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
                    var one = aPrimitives[i];
                    for (int j = b.Start; j < b.Start + b.Count; j++)
                    {
                        var two = bPrimitives[j];
                        if (one.ElementId == two.ElementId || !Overlaps(one.Bounds, two.Bounds, margin)) continue;
                        var key = (Math.Min(one.ElementId, two.ElementId), Math.Max(one.ElementId, two.ElementId));
                        bool found = contacts.TryGetValue(key, out var best);
                        switch (query)
                        {
                            case Query.Hard:
                                double depth = OverlapDepth(Overlap(meshBounds![one.MeshIndex], meshBounds[two.MeshIndex]));
                                if ((depth == 0 || depth >= limit) && (!found || -depth < best.Distance)
                                    && Contact(one, two) is { } hardPoint)
                                    contacts[key] = (hardPoint, -depth);
                                break;
                            case Query.Overlap:
                                var overlap = Overlap(one.Bounds, two.Bounds);
                                double overlapDepth = OverlapDepth(overlap);
                                if (!found || -overlapDepth < best.Distance)
                                    contacts[key] = (Midpoint(overlap.Min, overlap.Max), -overlapDepth);
                                break;
                            case Query.Duplicate:
                                if (!found && AreDuplicates(scene.Instances[one.MeshIndex], scene.Instances[two.MeshIndex],
                                    geometries, limit, cancellationToken))
                                {
                                    var centreA = Midpoint(one.Bounds.Min, one.Bounds.Max);
                                    var centreB = Midpoint(two.Bounds.Min, two.Bounds.Max);
                                    contacts[key] = (Midpoint(centreA, centreB), 0);
                                }
                                break;
                            case Query.Clearance:
                                if (best.Distance == 0) break;
                                if (best.Distance is double distance
                                    && !Overlaps(one.Bounds, two.Bounds, Math.Max(Tolerance, distance))) break;
                                if (Overlaps(one.Bounds, two.Bounds, Tolerance) && Contact(one, two) is { } contact)
                                {
                                    contacts[key] = (contact, 0);
                                    break;
                                }
                                var clearance = ClosestBetween(one, two);
                                if (clearance.Distance <= limit && (!found || clearance.Distance < best.Distance))
                                    contacts[key] = clearance;
                                break;
                            case Query.Intersection:
                                if (!found && Contact(one, two) is { } point)
                                    contacts[key] = (point, null);
                                break;
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

    private static Node Build(Primitive[] primitives, int start, int count, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var bounds = BoundingBox3d.Empty;
        for (int i = start; i < start + count; i++) bounds = bounds.Encapsulate(primitives[i].Bounds);
        if (count <= LeafSize) return new(bounds, start, count);
        Vector3d extent = bounds.Size;
        var comparer = extent.X >= extent.Y && extent.X >= extent.Z ? ByX : extent.Y >= extent.Z ? ByY : ByZ;
        Array.Sort(primitives, start, count, comparer);
        int half = count / 2;
        var left = Build(primitives, start, half, token);
        var right = Build(primitives, start + half, count - half, token);
        return new(bounds, start, count, left, right);
    }

    private static bool Overlaps(BoundingBox3d a, BoundingBox3d b, double margin) =>
        a.Min.X <= b.Max.X + margin && b.Min.X <= a.Max.X + margin
        && a.Min.Y <= b.Max.Y + margin && b.Min.Y <= a.Max.Y + margin
        && a.Min.Z <= b.Max.Z + margin && b.Min.Z <= a.Max.Z + margin;

    private static Vector3d? Contact(Primitive a, Primitive b) =>
        EdgeContact(a.A, a.B, b) ?? EdgeContact(a.B, a.C, b) ?? EdgeContact(a.C, a.A, b)
        ?? EdgeContact(b.A, b.B, a) ?? EdgeContact(b.B, b.C, a) ?? EdgeContact(b.C, b.A, a);

    private static Vector3d? EdgeContact(Vector3d from, Vector3d to, Primitive triangle)
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

    private static bool Inside(Vector3d point, Primitive triangle)
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

    private static readonly Comparer<Primitive> ByX = Comparer<Primitive>.Create((a, b) => a.Bounds.Center.X.CompareTo(b.Bounds.Center.X));
    private static readonly Comparer<Primitive> ByY = Comparer<Primitive>.Create((a, b) => a.Bounds.Center.Y.CompareTo(b.Bounds.Center.Y));
    private static readonly Comparer<Primitive> ByZ = Comparer<Primitive>.Create((a, b) => a.Bounds.Center.Z.CompareTo(b.Bounds.Center.Z));

    private enum Query { Intersection, Clearance, Hard, Overlap, Duplicate }

    private readonly record struct Primitive(long ElementId, BoundingBox3d Bounds,
        Vector3d A = default, Vector3d B = default, Vector3d C = default, Vector3d Normal = default, int MeshIndex = -1);
    private sealed record Node(BoundingBox3d Bounds, int Start, int Count, Node? First = null, Node? Second = null);
}
