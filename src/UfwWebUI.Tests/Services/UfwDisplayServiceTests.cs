using FluentAssertions;
using UfwWebUI.Services;

namespace UfwWebUI.Tests.Services;

public sealed class UfwDisplayServiceTests
{
    private readonly UfwDisplayService _service = new();

    [Theory]
    [InlineData(null, "any")]
    [InlineData("", "any")]
    [InlineData("  ", "any")]
    [InlineData("any", "any")]
    [InlineData("ANY", "any")]
    [InlineData("Any", "any")]
    public void GetDisplayValue_ShouldReturnAnyForNullOrAnyValues(string? input, string expected)
    {
        // Act
        string result = _service.GetDisplayValue(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("192.168.1.1", "192.168.1.1")]
    [InlineData("10.0.0.0/24", "10.0.0.0/24")]
    [InlineData("eth0", "eth0")]
    public void GetDisplayValue_ShouldReturnValueForNonAnyValues(string input, string expected)
    {
        // Act
        string result = _service.GetDisplayValue(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("any", true)]
    [InlineData("ANY", true)]
    [InlineData("Any", true)]
    [InlineData("192.168.1.1", false)]
    [InlineData("eth0", false)]
    public void IsAnyValue_ShouldCorrectlyIdentifyAnyValues(string? input, bool expected)
    {
        // Act
        bool result = _service.IsAnyValue(input);

        // Assert
        result.Should().Be(expected);
    }
}
