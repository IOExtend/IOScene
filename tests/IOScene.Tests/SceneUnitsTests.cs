namespace IOScene.Tests;

public class SceneUnitsTests
{
    [Theory]
    [InlineData("m")]
    [InlineData("m2")]
    [InlineData("m3")]
    [InlineData("rad")]
    [InlineData("kg")]
    [InlineData("s")]
    [InlineData("K")]
    [InlineData("m/s")]
    [InlineData("m/s2")]
    [InlineData("m3/s")]
    [InlineData("kg/m3")]
    [InlineData("N.m")]
    [InlineData("W/m2.K")]
    [InlineData("W/m.K")]
    [InlineData("kg.m2/s3.A")]
    [InlineData("J/kg.K")]
    [InlineData("Pa.s")]
    [InlineData("lm/W")]
    [InlineData("ohm.m")]
    [InlineData("m10")]
    [InlineData("A")]
    [InlineData("V")]
    [InlineData("Hz")]
    [InlineData("lx")]
    [InlineData("cd/m2")]
    [InlineData("mol/m3")]
    public void Valid(string unit) => Assert.True(SceneUnits.IsValid(unit));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("m^2")]            // no caret
    [InlineData("m²")]             // no superscript digits
    [InlineData("mm")]             // no prefixes
    [InlineData("km")]
    [InlineData("kPa")]
    [InlineData("ft")]             // SI only
    [InlineData("in")]
    [InlineData("deg")]
    [InlineData("°C")]
    [InlineData("m/s/s")]          // at most one slash
    [InlineData("W/(m2.K)")]       // no brackets
    [InlineData("m0")]             // exponents are positive
    [InlineData("m02")]
    [InlineData("m-1")]
    [InlineData("1/s")]
    [InlineData("2")]
    [InlineData("m.")]
    [InlineData(".m")]
    [InlineData("m/")]
    [InlineData("/s")]
    [InlineData("m s")]
    [InlineData("m*s")]
    [InlineData("M")]              // symbols are case-sensitive
    [InlineData("KG")]
    [InlineData("Ohm")]
    [InlineData("Ω")]
    [InlineData("m 2")]
    public void Invalid(string unit) => Assert.False(SceneUnits.IsValid(unit));

    [Fact]
    public void The_constants_follow_the_grammar() =>
        Assert.All(
            new[] { SceneUnits.Metre, SceneUnits.SquareMetre, SceneUnits.CubicMetre, SceneUnits.Radian, SceneUnits.Kilogram, SceneUnits.Second, SceneUnits.Kelvin },
            unit => Assert.True(SceneUnits.IsValid(unit), unit));

    [Fact]
    public void Null_is_not_a_unit_string() => Assert.Throws<ArgumentNullException>(() => SceneUnits.IsValid(null!));
}
