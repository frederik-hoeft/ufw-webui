using FluentAssertions;
using UfwWebUI.Models;
using UfwWebUI.Pipeline.Normalizers;

namespace UfwWebUI.Tests.Pipeline.Normalizers;

public sealed class TrimWhitespaceNormalizerTests
{
    private readonly TrimWhitespaceNormalizer _normalizer = new();

    [Fact]
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
        rule.Source.Should().Be("192.168.1.0/24");
        rule.Target.Should().Be("10.0.0.1");
        rule.Ports.Should().Be("80,443");
        rule.Interface.Should().Be("eth0");
        rule.Comment.Should().Be("Test comment");
    }

    [Fact]
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
        rule.Source.Should().BeNull();
        rule.Target.Should().BeNull();
        rule.Ports.Should().BeNull();
        rule.Interface.Should().BeNull();
        rule.Comment.Should().BeNull();
    }

    [Fact]
    public void Priority_ShouldBe1()
    {
        // Assert
        _normalizer.Priority.Should().Be(1);
    }
}
