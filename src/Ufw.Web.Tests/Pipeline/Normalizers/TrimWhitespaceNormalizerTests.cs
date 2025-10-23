using Microsoft.VisualStudio.TestTools.UnitTesting;
using UfwWebUI.Models;
using UfwWebUI.Pipeline.Normalizers;

namespace Ufw.Web.Tests.Pipeline.Normalizers;

[TestClass]
public sealed class TrimWhitespaceNormalizerTests
{
    private readonly TrimWhitespaceNormalizer _normalizer = new();

    [TestMethod]
    public void Normalize_ShouldTrimWhitespaceFromAllFields()
    {
        // Arrange
        UfwRule rule = new()
        {
            Source = "  192.168.1.0/24  ",
            Target = "  10.0.0.1  ",
            Ports = "  80,443  ",
            Interface = "  eth0  ",
            Comment = "  Test comment  "
        };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        Assert.AreEqual("192.168.1.0/24", rule.Source);
        Assert.AreEqual("10.0.0.1", rule.Target);
        Assert.AreEqual("80,443", rule.Ports);
        Assert.AreEqual("eth0", rule.Interface);
        Assert.AreEqual("Test comment", rule.Comment);
    }

    [TestMethod]
    public void Normalize_ShouldSetNullForEmptyStrings()
    {
        // Arrange
        UfwRule rule = new()
        {
            Source = "   ",
            Target = "",
            Ports = "\t",
            Interface = "  \n  ",
            Comment = ""
        };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        Assert.IsNull(rule.Source);
        Assert.IsNull(rule.Target);
        Assert.IsNull(rule.Ports);
        Assert.IsNull(rule.Interface);
        Assert.IsNull(rule.Comment);
    }

    [TestMethod]
    public void Priority_ShouldBe1() =>
        // Assert
        Assert.AreEqual(1, _normalizer.Priority);
}
