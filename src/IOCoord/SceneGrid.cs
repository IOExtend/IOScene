using IOScene;

namespace IOCoord;

/// <summary>A grid axis and the distance to it in the XY plane.</summary>
/// <param name="Line">The nearest axis.</param>
/// <param name="Distance">Distance in metres.</param>
public readonly record struct GridLineMatch(SceneGridLine Line, double Distance);

/// <summary>A unique intersection of two axes in the XY plane.</summary>
/// <param name="First">First axis.</param>
/// <param name="Second">Second axis.</param>
/// <param name="Point">The intersection in scene XY coordinates.</param>
/// <param name="Distance">Distance from the query in metres.</param>
public readonly record struct GridIntersection(SceneGridLine First, SceneGridLine Second, Vector2d Point, double Distance);

/// <summary>Queries the projected XY axes of a scene grid.</summary>
public static class SceneGrid
{
    /// <summary>Finds the nearest axis in the XY plane, or null when the grid has no axes of nonzero length.</summary>
    /// <param name="grid">The scene grid to search.</param>
    /// <param name="point">Query point in scene metres.</param>
    /// <param name="segmentOnly">Use the stored segment lengths instead of extending axes.</param>
    public static GridLineMatch? NearestLine(SceneGridSystem grid, Vector2d point, bool segmentOnly = false)
    {
        ArgumentNullException.ThrowIfNull(grid);
        RequireFinite(point);
        GridLineMatch? best = null;
        foreach (var line in grid.Lines)
        {
            var start = XY(line.Start);
            var direction = XY(line.End - line.Start);
            double lengthSquared = Dot(direction, direction);
            if (lengthSquared == 0) continue;
            double t = Dot(new(point.X - start.X, point.Y - start.Y), direction) / lengthSquared;
            if (segmentOnly) t = Math.Clamp(t, 0, 1);
            double distance = Distance(point, new(start.X + t * direction.X, start.Y + t * direction.Y));
            if (best is null || distance < best.Value.Distance) best = new(line, distance);
        }
        return best;
    }

    /// <summary>Finds the nearest crossing of extended axes in the XY plane, or null when there is no crossing.</summary>
    /// <param name="grid">The scene grid to search.</param>
    /// <param name="point">Query point in scene metres.</param>
    public static GridIntersection? NearestIntersection(SceneGridSystem grid, Vector2d point)
    {
        ArgumentNullException.ThrowIfNull(grid);
        RequireFinite(point);
        GridIntersection? best = null;
        for (int i = 0; i < grid.Lines.Count; i++)
        {
            var first = grid.Lines[i];
            var a = XY(first.Start);
            var u = XY(first.End - first.Start);
            for (int j = i + 1; j < grid.Lines.Count; j++)
            {
                var second = grid.Lines[j];
                var b = XY(second.Start);
                var v = XY(second.End - second.Start);
                double denominator = Cross(u, v);
                if (denominator == 0) continue;
                double t = Cross(new(b.X - a.X, b.Y - a.Y), v) / denominator;
                var intersection = new Vector2d(a.X + t * u.X, a.Y + t * u.Y);
                double distance = Distance(point, intersection);
                if (best is null || distance < best.Value.Distance) best = new(first, second, intersection, distance);
            }
        }
        return best;
    }

    private static Vector2d XY(Vector3d point)
    {
        var xy = new Vector2d(point.X, point.Y);
        RequireFinite(xy);
        return xy;
    }

    private static void RequireFinite(Vector2d point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            throw new ArgumentException("Grid coordinates must be finite.");
    }

    private static double Distance(Vector2d a, Vector2d b) => SceneMeasure.Distance(new(a.X, a.Y, 0), new(b.X, b.Y, 0));

    private static double Dot(Vector2d a, Vector2d b) => a.X * b.X + a.Y * b.Y;

    private static double Cross(Vector2d a, Vector2d b) => a.X * b.Y - a.Y * b.X;
}
