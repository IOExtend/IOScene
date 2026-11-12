using System.Globalization;
using System.Text.Json.Serialization;

namespace IOScene;

/// <summary>
/// 2D double-precision vector. In JSON it is an array of two numbers.
/// </summary>
/// <param name="X">X component.</param>
/// <param name="Y">Y component.</param>
[JsonConverter(typeof(NumberArrayConverter<Vector2d, Vector2dNumbers>))]
public readonly record struct Vector2d(double X, double Y)
{
    /// <summary>The vector (0, 0).</summary>
    public static readonly Vector2d Zero = new(0, 0);

    /// <summary>Returns the components in the invariant culture, as <c>(X, Y)</c>.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X}, {Y})");
}

/// <summary>
/// 3D double-precision vector or point. In a scene it is in metres, right-handed, Z up. In JSON it is an array of
/// three numbers.
/// </summary>
/// <param name="X">X component.</param>
/// <param name="Y">Y component.</param>
/// <param name="Z">Z component (up).</param>
[JsonConverter(typeof(NumberArrayConverter<Vector3d, Vector3dNumbers>))]
public readonly record struct Vector3d(double X, double Y, double Z)
{
    /// <summary>The vector (0, 0, 0).</summary>
    public static readonly Vector3d Zero = new(0, 0, 0);

    /// <summary>The vector (1, 0, 0).</summary>
    public static readonly Vector3d UnitX = new(1, 0, 0);

    /// <summary>The vector (0, 1, 0).</summary>
    public static readonly Vector3d UnitY = new(0, 1, 0);

    /// <summary>The vector (0, 0, 1), the up direction of a scene.</summary>
    public static readonly Vector3d UnitZ = new(0, 0, 1);

    /// <summary>Squared length.</summary>
    public double LengthSquared => X * X + Y * Y + Z * Z;

    /// <summary>Length.</summary>
    public double Length => Math.Sqrt(LengthSquared);

    /// <summary>Returns the vector scaled to length 1, or <see cref="Zero"/> when the length is below 1e-12.</summary>
    public Vector3d Normalized()
    {
        double len = Length;
        return len > 1e-12 ? new Vector3d(X / len, Y / len, Z / len) : Zero;
    }

    /// <summary>Component-wise sum.</summary>
    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>Component-wise difference.</summary>
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>Negation.</summary>
    public static Vector3d operator -(Vector3d v) => new(-v.X, -v.Y, -v.Z);

    /// <summary>Product of a vector and a number.</summary>
    public static Vector3d operator *(Vector3d v, double s) => new(v.X * s, v.Y * s, v.Z * s);

    /// <summary>Product of a number and a vector.</summary>
    public static Vector3d operator *(double s, Vector3d v) => new(v.X * s, v.Y * s, v.Z * s);

    /// <summary>Quotient of a vector and a number.</summary>
    public static Vector3d operator /(Vector3d v, double s) => new(v.X / s, v.Y / s, v.Z / s);

    /// <summary>Dot product.</summary>
    public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    /// <summary>Cross product (right-handed).</summary>
    public static Vector3d Cross(Vector3d a, Vector3d b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    /// <summary>Distance between two points.</summary>
    public static double Distance(Vector3d a, Vector3d b) => (a - b).Length;

    /// <summary>Returns the components in the invariant culture, as <c>(X, Y, Z)</c>.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X}, {Y}, {Z})");
}

/// <summary>
/// Axis-aligned bounding box.
/// </summary>
/// <param name="Min">Corner with the smallest coordinates.</param>
/// <param name="Max">Corner with the largest coordinates.</param>
public readonly record struct BoundingBox3d(Vector3d Min, Vector3d Max)
{
    /// <summary>The box that holds no point: <see cref="Min"/> is positive infinity, <see cref="Max"/> negative infinity.</summary>
    public static readonly BoundingBox3d Empty = new(
        new Vector3d(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity),
        new Vector3d(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity));

    /// <summary>True when the box holds no point.</summary>
    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    /// <summary>Centre of the box. Not meaningful for an empty box.</summary>
    public Vector3d Center => (Min + Max) * 0.5;

    /// <summary>Edge lengths of the box. Not meaningful for an empty box.</summary>
    public Vector3d Size => Max - Min;

    /// <summary>Returns the smallest box that holds this box and <paramref name="point"/>.</summary>
    /// <param name="point">Point to include.</param>
    public BoundingBox3d Encapsulate(Vector3d point)
    {
        if (IsEmpty) return new BoundingBox3d(point, point);
        return new BoundingBox3d(
            new Vector3d(Math.Min(Min.X, point.X), Math.Min(Min.Y, point.Y), Math.Min(Min.Z, point.Z)),
            new Vector3d(Math.Max(Max.X, point.X), Math.Max(Max.Y, point.Y), Math.Max(Max.Z, point.Z)));
    }

    /// <summary>Returns the smallest box that holds this box and <paramref name="other"/>.</summary>
    /// <param name="other">Box to include.</param>
    public BoundingBox3d Encapsulate(BoundingBox3d other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;
        return new BoundingBox3d(
            new Vector3d(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
            new Vector3d(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z)));
    }
}

/// <summary>
/// Affine 3D transform stored by columns: the result of <see cref="Apply"/> is
/// <c>XAxis * p.X + YAxis * p.Y + ZAxis * p.Z + Translation</c>. Mirror and non-uniform scale are allowed.
/// In JSON it is an array of twelve numbers: the three axes, then the translation.
/// </summary>
/// <remarks>The translation is in metres, in the scene's Z-up coordinates.</remarks>
/// <param name="XAxis">Image of the unit X vector (first matrix column).</param>
/// <param name="YAxis">Image of the unit Y vector (second matrix column).</param>
/// <param name="ZAxis">Image of the unit Z vector (third matrix column).</param>
/// <param name="Translation">Translation applied after the linear part.</param>
[JsonConverter(typeof(NumberArrayConverter<Transform3d, Transform3dNumbers>))]
public readonly record struct Transform3d(Vector3d XAxis, Vector3d YAxis, Vector3d ZAxis, Vector3d Translation)
{
    /// <summary>The transform that leaves every point unchanged.</summary>
    public static readonly Transform3d Identity = new(Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ, Vector3d.Zero);

    /// <summary>Creates a pure translation.</summary>
    /// <param name="t">Translation vector.</param>
    public static Transform3d Translate(Vector3d t) => new(Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ, t);

    /// <summary>Transforms a point.</summary>
    /// <param name="p">Point in local coordinates.</param>
    public Vector3d Apply(Vector3d p) => XAxis * p.X + YAxis * p.Y + ZAxis * p.Z + Translation;

    /// <summary>Composes two transforms: this one is applied first, then <paramref name="next"/>.</summary>
    /// <param name="next">Transform applied after this one.</param>
    public Transform3d Then(Transform3d next) => new(
        next.Apply(XAxis) - next.Translation,
        next.Apply(YAxis) - next.Translation,
        next.Apply(ZAxis) - next.Translation,
        next.Apply(Translation));
}

/// <summary>
/// Colour with 8 bits per channel. Red, green and blue are sRGB-encoded. Alpha is straight (not premultiplied)
/// linear coverage and the only opacity in a scene. In JSON it is an array of four integers from 0 to 255.
/// </summary>
/// <param name="R">Red.</param>
/// <param name="G">Green.</param>
/// <param name="B">Blue.</param>
/// <param name="A">Alpha: 255 is opaque, 0 is fully transparent.</param>
[JsonConverter(typeof(NumberArrayConverter<RgbaColor, RgbaColorNumbers>))]
public readonly record struct RgbaColor(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>Opaque white.</summary>
    public static readonly RgbaColor White = new(255, 255, 255);

    /// <summary>Opaque black.</summary>
    public static readonly RgbaColor Black = new(0, 0, 0);

    /// <summary>Opaque light grey (180, 180, 180).</summary>
    public static readonly RgbaColor Gray = new(180, 180, 180);

    /// <summary>Red as a number from 0 to 1.</summary>
    public double Rf => R / 255.0;

    /// <summary>Green as a number from 0 to 1.</summary>
    public double Gf => G / 255.0;

    /// <summary>Blue as a number from 0 to 1.</summary>
    public double Bf => B / 255.0;

    /// <summary>Alpha as a number from 0 to 1.</summary>
    public double Af => A / 255.0;

    /// <summary>Returns the colour without alpha as <c>#rrggbb</c>.</summary>
    public string ToHex() => string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}");

    /// <summary>Returns the colour in the invariant culture, as <c>rgba(R, G, B, alpha)</c> with alpha from 0 to 1.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"rgba({R}, {G}, {B}, {Af:F2})");
}
