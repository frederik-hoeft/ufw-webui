using Microsoft.VisualStudio.TestTools.UnitTesting;
using UfwWebUI.Models;
using UfwWebUI.Pipeline.Normalizers;

namespace UfwWebUI.Tests.Pipeline.Normalizers;

[TestClass]
public sealed class PortRangeNormalizerTests
{
    private readonly PortRangeNormalizer _normalizer = new();

    [TestMethod]
    [DataRow("80, 443", "80,443")]
    [DataRow("8080: 8090", "8080:8090")]
    [DataRow("  21 ,  60000 : 60100  ", "21,60000:60100")]
    [DataRow("80 , 443 , 8080 : 8090", "80,443,8080:8090")]
    public void Normalize_ShouldRemoveAllWhitespace(string input, string expected)
    {
        // Arrange
        UfwRule rule = new() { Ports = input };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        Assert.AreEqual(expected, rule.Ports);
    }

    [TestMethod]
    public void Normalize_ShouldNotChangePortsWithoutWhitespace()
    {
        // Arrange
        UfwRule rule = new() { Ports = "80,443,8080:8090" };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        Assert.AreEqual("80,443,8080:8090", rule.Ports);
    }

    [TestMethod]
    public void Normalize_ShouldHandleNullPorts()
    {
        // Arrange
        UfwRule rule = new() { Ports = null };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        Assert.IsNull(rule.Ports);
    }

    [TestMethod]
    public void Priority_ShouldBe3() =>
        // Assert
        Assert.AreEqual(3, _normalizer.Priority);
}
