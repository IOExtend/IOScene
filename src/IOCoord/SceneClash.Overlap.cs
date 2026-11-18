using IOScene;

namespace IOCoord;

public static partial class SceneClash
{
    /// <summary>Finds surface clashes using the overlap of the colliding mesh bounds to estimate depth.</summary>
    /// <param name="scene">The scene whose placed triangles are tested.</param>
    /// <param name="first">Ids of elements in the first selection.</param>
    /// <param name="second">Ids of elements in the second selection.</param>
    /// <param name="minDepthMetres">Inclusive minimum positive depth in metres, finite and nonnegative.</param>
    /// <param name="cancellationToken">Cancellation token for the query.</param>
    /// <returns>The deepest contact per element pair, with a signed distance of zero or less in metres.</returns>
    /// <remarks>Zero-depth contacts are retained. Depth is the minimum side length of the overlap of the two
    /// contacting instances' axis-aligned bounds. Enclosed solids whose surfaces do not touch have no result.</remarks>
    public static SceneClashTest FindHardClashes(SceneDocument scene, IEnumerable<long> first, IEnumerable<long> second,
        double minDepthMetres = 0, CancellationToken cancellationToken = default)
    {
        ValidateDistance(minDepthMetres, nameof(minDepthMetres));
        return Find(scene, first, second, Query.Hard, minDepthMetres, cancellationToken);
    }

    /// <summary>Finds potentially clashing element pairs by comparing the bounds of their placed geometry.</summary>
    /// <param name="scene">The scene whose placed geometry is tested.</param>
    /// <param name="first">Ids of elements in the first selection.</param>
    /// <param name="second">Ids of elements in the second selection.</param>
    /// <param name="cancellationToken">Cancellation token for the query.</param>
    /// <returns>The deepest box overlap per pair, with the centre of that overlap and its negative depth in metres.</returns>
    /// <remarks>Touching boxes have a distance of zero. The box overlap can include empty space.</remarks>
    public static SceneClashTest FindBoundingBoxOverlaps(SceneDocument scene, IEnumerable<long> first, IEnumerable<long> second,
        CancellationToken cancellationToken = default) =>
        Find(scene, first, second, Query.Overlap, 0, cancellationToken);

    private static BoundingBox3d Overlap(BoundingBox3d a, BoundingBox3d b) => new(
        new(Math.Max(a.Min.X, b.Min.X), Math.Max(a.Min.Y, b.Min.Y), Math.Max(a.Min.Z, b.Min.Z)),
        new(Math.Min(a.Max.X, b.Max.X), Math.Min(a.Max.Y, b.Max.Y), Math.Min(a.Max.Z, b.Max.Z)));

    private static double OverlapDepth(BoundingBox3d overlap)
    {
        if (overlap.IsEmpty) return 0;
        var size = overlap.Size;
        double depth = Math.Min(size.X, Math.Min(size.Y, size.Z));
        if (!double.IsFinite(depth)) throw new InvalidDataException("The overlap depth is outside the finite range.");
        return depth;
    }

    private static Vector3d Midpoint(Vector3d a, Vector3d b) => a * 0.5 + b * 0.5;

    private static void ValidateDistance(double distance, string parameter)
    {
        if (distance < 0 || !double.IsFinite(distance))
            throw new ArgumentOutOfRangeException(parameter, "The distance must be finite and nonnegative.");
    }
}
