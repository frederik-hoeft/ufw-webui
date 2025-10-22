using FluentAssertions;
using UfwWebUI.Models;
using UfwWebUI.Pipeline.Normalizers;

namespace UfwWebUI.Tests.Pipeline.Normalizers;

public sealed class PortRangeNormalizerTests
{
    private readonly PortRangeNormalizer _normalizer = new();

    [Theory]
    [InlineData("80, 443", "80,443")]
    [InlineData("8080: 8090", "8080:8090")]
    [InlineData("  21 ,  60000 : 60100  ", "21,60000:60100")]
    [InlineData("80 , 443 , 8080 : 8090", "80,443,8080:8090")]
    public void Normalize_ShouldRemoveAllWhitespace(string input, string expected)
    {
        // Arrange
        UfwRule rule = new() { Ports = input };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        rule.Ports.Should().Be(expected);
    }

    [Fact]
    public void Normalize_ShouldNotChangePortsWithoutWhitespace()
    {
        // Arrange
        UfwRule rule = new() { Ports = "80,443,8080:8090" };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        rule.Ports.Should().Be("80,443,8080:8090");
    }

    [Fact]
    public void Normalize_ShouldHandleNullPorts()
    {
        // Arrange
        UfwRule rule = new() { Ports = null };

        // Act
        _normalizer.Normalize(rule);

        // Assert
        rule.Ports.Should().BeNull();
    }

    [Fact]
    public void Priority_ShouldBe3()
    {
        // Assert
        _normalizer.Priority.Should().Be(3);
    }
}
