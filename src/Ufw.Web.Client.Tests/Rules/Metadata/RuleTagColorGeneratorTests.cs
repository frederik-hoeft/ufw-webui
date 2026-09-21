using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Rules.Metadata;

[TestClass]
public sealed class RuleTagColorGeneratorTests
{
    [TestMethod]
    [DataRow(0d)]
    [DataRow(60d)]
    [DataRow(120d)]
    [DataRow(180d)]
    [DataRow(240d)]
    [DataRow(300d)]
    public void FromHue_ReturnsCanonicalRgbColor(double hue)
    {
        string color = RuleTagColorGenerator.FromHue(hue);

        Assert.AreEqual(7, color.Length);
        Assert.AreEqual('#', color[0]);
        Assert.IsTrue(color[1..].All(static character => char.IsAsciiHexDigit(character) && !char.IsLower(character)));
        Assert.IsTrue(RuleTagColor.TryNormalize(color, out string? normalized));
        Assert.AreEqual(color, normalized);
    }

    [TestMethod]
    public void FromHue_ProducesDistinctHighSaturationPaletteAcrossPrimaryHueSectors()
    {
        double[] hues = [0d, 60d, 120d, 180d, 240d, 300d];
        string[] colors = hues.Select(RuleTagColorGenerator.FromHue).ToArray();

        Assert.AreEqual(colors.Length, colors.Distinct(StringComparer.Ordinal).Count());
    }
}
