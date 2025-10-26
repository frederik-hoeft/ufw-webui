using Ufw.Web.Models;
using Ufw.Web.Pipeline.Normalizers;

namespace Ufw.Web.Tests.Pipeline.Normalizers;

[TestClass]
public sealed class AnyValueNormalizerTests
{
    private readonly AnyValueNormalizer _normalizer = new();

    [TestMethod]
    [DataRow("any", "any")]
    [DataRow("ANY", "any")]
    [DataRow("Any", "any")]
    [DataRow("aNy", "any")]
    public void Normalize_ShouldNormalizeAnyToLowercase(string input, string expected)
    {
        // Arrange
        UfwRule rule = new() { Source = input, Target = input };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        Assert.AreEqual(expected, rule.Source);
        Assert.AreEqual(expected, rule.Target);
    }

    [TestMethod]
    public void Normalize_ShouldFillBlankWithAny()
    {
        // Arrange
        UfwRule rule = new() { Source = null, Target = "" };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        Assert.AreEqual("any", rule.Source);
        Assert.AreEqual("any", rule.Target);
    }

    [TestMethod]
    public void Normalize_ShouldNotChangeValidIPAddresses()
    {
        // Arrange
        UfwRule rule = new() { Source = "192.168.1.1", Target = "10.0.0.0/24" };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        Assert.AreEqual("192.168.1.1", rule.Source);
        Assert.AreEqual("10.0.0.0/24", rule.Target);
    }

    [TestMethod]
    public void Priority_ShouldBe2() =>
        // Assert
        Assert.AreEqual(2, _normalizer.Priority);
}
