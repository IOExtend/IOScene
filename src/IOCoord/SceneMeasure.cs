using IOScene;

namespace IOCoord;

/// <summary>Result of measuring from a point to the placed geometry of one element, in metres.</summary>
/// <param name="ClosestPoint">Closest point on the element in scene coordinates.</param>
/// <param name="Distance">Distance to that point in metres.</param>
public readonly record struct PointMeasurement(Vector3d ClosestPoint, double Distance);

/// <summary>Measurements on the metre-based geometry of an IOScene document.</summary>
public static class SceneMeasure
{
    /// <summary>Returns the straight-line distance between two points in metres.</summary>
    /// <param name="a">First point.</param>
    /// <param name="b">Second point.</param>
    public static double Distance(Vector3d a, Vector3d b)
    {
        RequireFinite(a, nameof(a));
        RequireFinite(b, nameof(b));
        return Length(a - b);
    }

    /// <summary>Returns the sum of the lengths of consecutive segments in metres.</summary>
    /// <param name="points">The points of an open path in order.</param>
    public static double PathLength(IEnumerable<Vector3d> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        using var iterator = points.GetEnumerator();
        if (!iterator.MoveNext()) return 0;
        Vector3d previous = iterator.Current;
        RequireFinite(previous, nameof(points));
        double length = 0;
        while (iterator.MoveNext())
        {
            var point = iterator.Current;
            RequireFinite(point, nameof(points));
            length += Length(point - previous);
            previous = point;
        }
        if (!double.IsFinite(length)) throw new InvalidDataException("The path length is outside the finite range.");
        return length;
    }

    /// <summary>Returns the angle at a vertex in degrees, or zero for a segment of length zero.</summary>
    /// <param name="a">First endpoint.</param>
    /// <param name="vertex">Common endpoint.</param>
    /// <param name="b">Second endpoint.</param>
    public static double AngleDegrees(Vector3d a, Vector3d vertex, Vector3d b)
    {
        RequireFinite(a, nameof(a));
        RequireFinite(vertex, nameof(vertex));
        RequireFinite(b, nameof(b));
        var first = a - vertex;
        var second = b - vertex;
        double firstLength = Length(first), secondLength = Length(second);
        if (firstLength == 0 || secondLength == 0) return 0;
        var x = first / firstLength;
        var y = second / secondLength;
        return Math.Atan2(Length(Vector3d.Cross(x, y)), Vector3d.Dot(x, y)) * (180 / Math.PI);
    }

    /// <summary>Measures to the nearest triangle, line segment or point placed for an element.</summary>
    /// <param name="scene">The scene to measure.</param>
    /// <param name="elementId">The scene-local id of the element.</param>
    /// <param name="point">Query point in scene coordinates.</param>
    /// <returns>The closest point and distance, or null when the element has no measurable geometry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The scene has no element with the supplied id.</exception>
    /// <exception cref="InvalidDataException">The scene fails structural validation or the transformed coordinates overflow.</exception>
    public static PointMeasurement? PointToElement(SceneDocument scene, long elementId, Vector3d point)
    {
        ArgumentNullException.ThrowIfNull(scene);
        RequireFinite(point, nameof(point));
        if (scene.Validate().FirstOrDefault(issue => issue.Severity == SceneSeverity.Error) is { } error)
            throw new InvalidDataException($"Invalid scene ({error.Code}): {error.Message}");
        if (!scene.Elements.Any(element => element.Id == elementId))
            throw new ArgumentOutOfRangeException(nameof(elementId), elementId, "The scene has no such element.");

        var geometries = scene.Geometries.ToDictionary(geometry => geometry.Id);
        PointMeasurement? best = null;
        foreach (var instance in scene.Instances)
        {
            if (instance.ElementId != elementId) continue;

            var geometry = geometries[instance.GeometryId];
            var transform = instance.Transform ?? Transform3d.Identity;
            bool indexed = geometry.Indices.Count > 0;
            int count = indexed ? geometry.Indices.Count : geometry.Vertices.Count;
            int step = geometry.Topology switch
            {
                SceneTopology.Points => 1,
                SceneTopology.Lines => 2,
                SceneTopology.Triangles => 3,
                _ => throw new InvalidDataException($"Geometry {geometry.Id} has an unsupported topology."),
            };
            for (int offset = 0; offset < count; offset += step)
            {
                Vector3d a = WorldVertex(offset);
                Vector3d nearest = step switch
                {
                    1 => a,
                    2 => ClosestOnSegment(point, a, WorldVertex(offset + 1)),
                    _ => ClosestOnTriangle(point, a, WorldVertex(offset + 1), WorldVertex(offset + 2)),
                };
                double distance = Length(point - nearest);
                if (best is null || distance < best.Value.Distance) best = new(nearest, distance);
            }

            Vector3d WorldVertex(int index)
            {
                Vector3d world = transform.Apply(geometry.Vertices[indexed ? geometry.Indices[index] : index]);
                if (!IsFinite(world)) throw new InvalidDataException("The transformed coordinates are outside the finite range.");
                return world;
            }
        }
        return best;
    }

    private static Vector3d ClosestOnSegment(Vector3d point, Vector3d a, Vector3d b)
    {
        var vector = b - a;
        double length = Length(vector);
        if (length == 0) return a;
        var unit = vector / length;
        double along = Math.Clamp(Vector3d.Dot(point - a, unit), 0, length);
        return a + unit * along;
    }

    internal static Vector3d ClosestOnTriangle(Vector3d point, Vector3d a, Vector3d b, Vector3d c)
    {
        var ab = b - a;
        var ac = c - a;
        double scale = Math.Max(Length(ab), Length(ac));
        if (scale == 0) return a;

        var normal = Vector3d.Cross(ab / scale, ac / scale);
        double normalLength = Length(normal);
        if (normalLength > 0)
        {
            normal /= normalLength;
            var projection = point - normal * Vector3d.Dot(point - a, normal);
            if (OnInside(a, b) && OnInside(b, c) && OnInside(c, a)) return projection;

            bool OnInside(Vector3d start, Vector3d end) =>
                Vector3d.Dot(Vector3d.Cross((end - start) / scale, (projection - start) / scale), normal) >= 0;
        }

        var closest = ClosestOnSegment(point, a, b);
        var next = ClosestOnSegment(point, b, c);
        if (Length(next - point) < Length(closest - point)) closest = next;
        next = ClosestOnSegment(point, c, a);
        return Length(next - point) < Length(closest - point) ? next : closest;
    }

    internal static double Length(Vector3d vector)
    {
        double scale = Math.Max(Math.Abs(vector.X), Math.Max(Math.Abs(vector.Y), Math.Abs(vector.Z)));
        if (scale == 0) return 0;
        double length = (vector / scale).Length * scale;
        if (!double.IsFinite(length)) throw new InvalidDataException("The distance is outside the finite range.");
        return length;
    }

    private static void RequireFinite(Vector3d value, string parameter)
    {
        if (!IsFinite(value)) throw new ArgumentException("Point coordinates must be finite.", parameter);
    }

    private static bool IsFinite(Vector3d value) =>
        double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);
}
