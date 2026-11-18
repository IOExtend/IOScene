using IOScene;

namespace IOCoord;

public static partial class SceneClash
{
    /// <summary>Finds element pairs with matching placed geometry and ordered topology.</summary>
    /// <param name="scene">The scene whose placed geometry is tested.</param>
    /// <param name="first">Ids of elements in the first selection.</param>
    /// <param name="second">Ids of elements in the second selection.</param>
    /// <param name="toleranceMetres">Inclusive maximum distance between corresponding vertices, in metres.</param>
    /// <param name="cancellationToken">Cancellation token for the query.</param>
    /// <returns>One zero-distance result per element pair with at least one matching instance.</returns>
    /// <remarks>Topology, vertex order and effective index order must match. Material and names do not affect the query.</remarks>
    public static SceneClashTest FindDuplicates(SceneDocument scene, IEnumerable<long> first, IEnumerable<long> second,
        double toleranceMetres = 0, CancellationToken cancellationToken = default)
    {
        ValidateDistance(toleranceMetres, nameof(toleranceMetres));
        return Find(scene, first, second, Query.Duplicate, toleranceMetres, cancellationToken);
    }

    private static bool AreDuplicates(SceneInstance first, SceneInstance second,
        Dictionary<int, SceneGeometry> geometries, double tolerance, CancellationToken token)
    {
        var a = geometries[first.GeometryId];
        var b = geometries[second.GeometryId];
        if (a.Topology != b.Topology || a.Vertices.Count != b.Vertices.Count) return false;

        int aCount = a.Indices.Count == 0 ? a.Vertices.Count : a.Indices.Count;
        int bCount = b.Indices.Count == 0 ? b.Vertices.Count : b.Indices.Count;
        if (aCount != bCount) return false;
        if (a.Id != b.Id)
        {
            for (int i = 0; i < aCount; i++)
            {
                token.ThrowIfCancellationRequested();
                int ai = a.Indices.Count == 0 ? i : a.Indices[i];
                int bi = b.Indices.Count == 0 ? i : b.Indices[i];
                if (ai != bi) return false;
            }
        }

        var transformA = first.Transform ?? Transform3d.Identity;
        var transformB = second.Transform ?? Transform3d.Identity;
        if (a.Id == b.Id && transformA == transformB) return true;
        for (int i = 0; i < a.Vertices.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var delta = transformA.Apply(a.Vertices[i]) - transformB.Apply(b.Vertices[i]);
            if (double.Hypot(double.Hypot(delta.X, delta.Y), delta.Z) > tolerance) return false;
        }
        return true;
    }
}
