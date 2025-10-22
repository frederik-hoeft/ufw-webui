using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UfwWebUI.Models;
using UfwWebUI.Pipeline.Normalizers;

namespace UfwWebUI.Tests.Pipeline.Normalizers;

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
        rule.Source.Should().Be(expected);
        rule.Target.Should().Be(expected);
    }

    [TestMethod]
    public void Normalize_ShouldFillBlankWithAny()
    {
        // Arrange
        UfwRule rule = new() { Source = null, Target = "" };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        rule.Source.Should().Be("any");
        rule.Target.Should().Be("any");
    }

    [TestMethod]
    public void Normalize_ShouldNotChangeValidIPAddresses()
    {
        // Arrange
        UfwRule rule = new() { Source = "192.168.1.1", Target = "10.0.0.0/24" };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        rule.Source.Should().Be("192.168.1.1");
        rule.Target.Should().Be("10.0.0.0/24");
    }

    [TestMethod]
    public void Priority_ShouldBe2() =>
        // Assert
        _normalizer.Priority.Should().Be(2);
}
