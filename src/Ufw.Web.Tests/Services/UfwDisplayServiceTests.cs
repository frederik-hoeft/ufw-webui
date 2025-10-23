using Microsoft.VisualStudio.TestTools.UnitTesting;
using UfwWebUI.Services;

namespace Ufw.Web.Tests.Services;

[TestClass]
public sealed class UfwDisplayServiceTests
{
    private readonly UfwDisplayService _service = new();

    [TestMethod]
    [DataRow(null, "any")]
    [DataRow("", "any")]
    [DataRow("  ", "any")]
    [DataRow("any", "any")]
    [DataRow("ANY", "any")]
    [DataRow("Any", "any")]
    public void GetDisplayValue_ShouldReturnAnyForNullOrAnyValues(string? input, string expected)
    {
        // Act
        string result = _service.GetDisplayValue(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("192.168.1.1", "192.168.1.1")]
    [DataRow("10.0.0.0/24", "10.0.0.0/24")]
    [DataRow("eth0", "eth0")]
    public void GetDisplayValue_ShouldReturnValueForNonAnyValues(string input, string expected)
    {
        // Act
        string result = _service.GetDisplayValue(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(null, true)]
    [DataRow("", true)]
    [DataRow("  ", true)]
    [DataRow("any", true)]
    [DataRow("ANY", true)]
    [DataRow("Any", true)]
    [DataRow("192.168.1.1", false)]
    [DataRow("eth0", false)]
    public void IsAnyValue_ShouldCorrectlyIdentifyAnyValues(string? input, bool expected)
    {
        // Act
        bool result = _service.IsAnyValue(input);

        // Assert
        Assert.AreEqual(expected, result);
    }
}
