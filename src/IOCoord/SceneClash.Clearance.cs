using IOScene;

namespace IOCoord;

public static partial class SceneClash
{
    /// <summary>Finds the minimum surface gap for each element pair within the specified clearance.</summary>
    /// <param name="scene">The scene whose placed triangles are tested.</param>
    /// <param name="first">Ids of elements in the first selection.</param>
    /// <param name="second">Ids of elements in the second selection.</param>
    /// <param name="maxClearanceMetres">Inclusive maximum distance in metres, finite and nonnegative.</param>
    /// <param name="cancellationToken">Cancellation token for the query.</param>
    /// <returns>One result per pair. Its point is the midpoint of the measured gap.</returns>
    /// <remarks>Intersecting surfaces report a distance of zero, using the same rounding tolerance as
    /// <see cref="FindSurfaceIntersections"/>. The query measures triangle surfaces.</remarks>
    public static SceneClashTest FindSurfaceClearances(SceneDocument scene, IEnumerable<long> first, IEnumerable<long> second,
        double maxClearanceMetres, CancellationToken cancellationToken = default)
    {
        ValidateDistance(maxClearanceMetres, nameof(maxClearanceMetres));
        return Find(scene, first, second, Query.Clearance, maxClearanceMetres, cancellationToken);
    }

    private static (Vector3d Point, double Distance) ClosestBetween(Primitive a, Primitive b)
    {
        ReadOnlySpan<Vector3d> first = [a.A, a.B, a.C];
        ReadOnlySpan<Vector3d> second = [b.A, b.B, b.C];
        Vector3d midpoint = default;
        double shortest = double.PositiveInfinity;

        // For disjoint triangles a minimum occurs at a vertex and a face, or between two edges.
        for (int i = 0; i < 3; i++)
        {
            Consider(first[i], SceneMeasure.ClosestOnTriangle(first[i], b.A, b.B, b.C));
            Consider(SceneMeasure.ClosestOnTriangle(second[i], a.A, a.B, a.C), second[i]);
            for (int j = 0; j < 3; j++)
            {
                var (one, two) = ClosestBetweenEdges(first[i], first[(i + 1) % 3], second[j], second[(j + 1) % 3]);
                Consider(one, two);
            }
        }
        return (midpoint, shortest);

        void Consider(Vector3d one, Vector3d two)
        {
            double distance = SceneMeasure.Length(one - two);
            if (distance >= shortest) return;
            shortest = distance;
            midpoint = Midpoint(one, two);
        }
    }

    private static (Vector3d First, Vector3d Second) ClosestBetweenEdges(
        Vector3d aStart, Vector3d aEnd, Vector3d bStart, Vector3d bEnd)
    {
        var a = aEnd - aStart;
        var b = bEnd - bStart;
        double aLength = SceneMeasure.Length(a);
        double bLength = SceneMeasure.Length(b);
        a /= aLength;
        b /= bLength;
        var cross = Vector3d.Cross(a, b);
        double denominator = Vector3d.Dot(cross, cross);
        var offset = bStart - aStart;
        double alongA = denominator == 0 ? 0
            : Math.Clamp(Vector3d.Dot(Vector3d.Cross(offset, b), cross) / denominator, 0, aLength);
        double alongB = Math.Clamp(Vector3d.Dot(a * alongA - offset, b), 0, bLength);
        alongA = Math.Clamp(Vector3d.Dot(offset + b * alongB, a), 0, aLength);
        return (aStart + a * alongA, bStart + b * alongB);
    }
}
