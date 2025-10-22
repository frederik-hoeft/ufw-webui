using System.ComponentModel.DataAnnotations;
using UfwWebUI.Data.Validation;

namespace UfwWebUI.Tests.Data.Validation;

[TestClass]
public sealed class ValidIPv4AddressOrAnyAttributeTests
{
    private readonly ValidIPv4AddressOrAnyAttribute _attribute = new();
    private readonly ValidationContext _context = new(new object()) { DisplayName = "TestField" };

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\t")]
    public void IsValid_NullOrWhitespace_ReturnsSuccess(string? input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    [DataRow("any")]
    [DataRow("ANY")]
    [DataRow("Any")]
    [DataRow("aNy")]
    [DataRow("  any  ")]
    [DataRow("\tany\t")]
    public void IsValid_AnyValue_ReturnsSuccess(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    [DataRow("0.0.0.0/0")]
    [DataRow("  0.0.0.0/0  ")]
    public void IsValid_ZeroNetwork_ReturnsSuccess(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    [DataRow("192.168.1.1")]
    [DataRow("10.0.0.1")]
    [DataRow("172.16.0.1")]
    [DataRow("1.2.3.4")]
    [DataRow("255.255.255.255")]
    [DataRow("0.0.0.0")]
    [DataRow("  192.168.1.1  ")]
    public void IsValid_ValidIPv4Address_ReturnsSuccess(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    [DataRow("192.168.1.0/24")]
    [DataRow("10.0.0.0/8")]
    [DataRow("172.16.0.0/16")]
    [DataRow("192.168.1.128/25")]
    [DataRow("10.0.0.0/0")]
    [DataRow("192.168.1.0/32")]
    [DataRow("  192.168.1.0/24  ")]
    [DataRow("192.168.1.0 / 24")]
    public void IsValid_ValidIPv4AddressWithCIDR_ReturnsSuccess(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    [DataRow("256.1.1.1")]
    [DataRow("1.256.1.1")]
    [DataRow("1.1.256.1")]
    [DataRow("1.1.1.256")]
    [DataRow("1000.0.0.0")]
    [DataRow("-1.0.0.0")]
    [DataRow("192.168.1")]
    [DataRow("192.168.1.1.1")]
    [DataRow("abc.def.ghi.jkl")]
    [DataRow("not-an-ip")]
    public void IsValid_InvalidIPv4Address_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
        Assert.Contains("valid IPv4 address", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("192.168.1.0/33")]
    [DataRow("192.168.1.0/-1")]
    [DataRow("192.168.1.0/1234")]
    [DataRow("192.168.1.0/abc")]
    [DataRow("192.168.1.0/")]
    [DataRow("192.168.1.0/ ")]
    public void IsValid_InvalidSubnetMask_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
        Assert.Contains("subnet mask", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("256.1.1.1/24")]
    [DataRow("1000.0.0.0/16")]
    [DataRow("not-an-ip/24")]
    public void IsValid_InvalidIPv4AddressWithCIDR_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
        Assert.Contains("valid IPv4 address", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("192.168.1.0/24/32")]
    [DataRow("10.0.0.0/8/16")]
    public void IsValid_MultipleCIDRNotation_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("::1")]
    [DataRow("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
    [DataRow("fe80::1")]
    public void IsValid_IPv6Address_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
    }

    [TestMethod]
    public void IsValid_NullValidationContext_ThrowsArgumentNullException() =>
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _attribute.GetValidationResult("192.168.1.1", null!));
}