namespace IOScene;

/// <summary>
/// Unit strings of <see cref="SceneValue.Unit"/>. One ASCII grammar: coherent SI symbols from a fixed list,
/// <c>.</c> for a product, at most one <c>/</c>, positive integer exponents written after the symbol:
/// <c>m2</c>, <c>m3/s</c>, <c>W/m2.K</c>. There are no prefixes, no <c>^</c> and no superscript digits.
/// </summary>
/// <remarks>
/// The symbols are m, kg, s, A, K, mol, cd, rad, sr, Hz, N, Pa, J, W, C, V, F, ohm, S, Wb, T, H, lm and lx.
/// </remarks>
public static class SceneUnits
{
    /// <summary>Metre.</summary>
    public const string Metre = "m";

    /// <summary>Square metre.</summary>
    public const string SquareMetre = "m2";

    /// <summary>Cubic metre.</summary>
    public const string CubicMetre = "m3";

    /// <summary>Radian.</summary>
    public const string Radian = "rad";

    /// <summary>Kilogram.</summary>
    public const string Kilogram = "kg";

    /// <summary>Second.</summary>
    public const string Second = "s";

    /// <summary>Kelvin.</summary>
    public const string Kelvin = "K";

    private static readonly HashSet<string> Symbols =
    [
        "m", "kg", "s", "A", "K", "mol", "cd", "rad", "sr", "Hz", "N", "Pa", "J", "W", "C", "V", "F", "ohm", "S", "Wb", "T", "H", "lm", "lx",
    ];

    /// <summary>Tells whether a unit string follows the grammar.</summary>
    /// <param name="unit">Unit string to check.</param>
    /// <returns>True when <paramref name="unit"/> is a valid unit; false otherwise, the empty string included.</returns>
    public static bool IsValid(string unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        string[] sides = unit.Split('/');
        return sides.Length <= 2 && sides.All(side => side.Split('.').All(IsTerm));
    }

    // A symbol of the list, then an optional exponent without a leading zero.
    private static bool IsTerm(string term)
    {
        string symbol = term.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        return Symbols.Contains(symbol) && (term.Length == symbol.Length || term[symbol.Length] != '0');
    }
}
