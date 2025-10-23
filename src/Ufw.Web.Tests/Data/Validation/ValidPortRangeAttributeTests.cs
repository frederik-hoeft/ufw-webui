using System.ComponentModel.DataAnnotations;
using UfwWebUI.Data.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ufw.Web.Tests.Data.Validation;

[TestClass]
public sealed class ValidPortRangeAttributeTests
{
    private readonly ValidPortRangeAttribute _attribute = new();
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
    [DataRow("80")]
    [DataRow("443")]
    [DataRow("8080")]
    [DataRow("0")]
    [DataRow("65535")]
    [DataRow("  80  ")]
    [DataRow("\t443\t")]
    public void IsValid_SinglePort_ReturnsSuccess(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    [DataRow("80:82")]
    [DataRow("8080:8090")]
    [DataRow("60000:60100")]
    [DataRow("0:65535")]
    [DataRow("1:1")]
    [DataRow("  80:82  ")]
    [DataRow("80 : 82")]
    [DataRow("  80  :  82  ")]
    public void IsValid_PortRange_ReturnsSuccess(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    [DataRow("80,443")]
    [DataRow("80,443,8080")]
    [DataRow("22,80,443,8080")]
    [DataRow("80, 443")]
    [DataRow("80 , 443 , 8080")]
    [DataRow("  80  ,  443  ")]
    public void IsValid_CommaSeparatedPorts_ReturnsSuccess(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    [DataRow("80,443,8080:8090")]
    [DataRow("21,60000:60100")]
    [DataRow("80:82,443")]
    [DataRow("22,80:82,443,8080:8090")]
    [DataRow("10011,30033,41144")]
    [DataRow("  80:82  ,  443  ")]
    [DataRow("80 , 443 , 8080:8090")]
    public void IsValid_MixedPortsAndRanges_ReturnsSuccess(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("65536")]
    [DataRow("100000")]
    [DataRow("abc")]
    [DataRow("port")]
    [DataRow("80.5")]
    public void IsValid_InvalidSinglePort_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
        Assert.Contains("invalid port", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("65536:65537")]
    [DataRow("-1:80")]
    [DataRow("80:-1")]
    [DataRow("abc:def")]
    [DataRow("80:abc")]
    [DataRow("abc:80")]
    public void IsValid_InvalidPortRange_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("90:80")]
    [DataRow("8090:8080")]
    [DataRow("65535:0")]
    [DataRow("100:50")]
    public void IsValid_PortRangeStartGreaterThanEnd_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
        Assert.Contains("start port is greater than end port", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("80:82:84")]
    [DataRow("80:82:84:86")]
    public void IsValid_MultipleColonsInRange_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
        Assert.Contains("invalid port range", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("80,65536")]
    [DataRow("443,-1")]
    [DataRow("80,abc")]
    [DataRow("80,443,invalid")]
    public void IsValid_CommaSeparatedWithInvalidPort_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("80,443,90:80")]
    [DataRow("21,65536:65537")]
    [DataRow("80:82,100:50")]
    public void IsValid_MixedWithInvalidRange_ReturnsError(string input)
    {
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreNotEqual(ValidationResult.Success, result);
        Assert.IsNotNull(result?.ErrorMessage);
        Assert.Contains("TestField", result.ErrorMessage);
    }

    [TestMethod]
    public void IsValid_EmptyPortAfterComma_ReturnsSuccess()
    {
        // Note: This is based on the current implementation which uses IndexOf(',')
        // If there's a trailing comma, it will be handled correctly
        string input = "80,";

        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert - The current implementation would handle this as just "80"
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    public void IsValid_NullValidationContext_ThrowsArgumentNullException() =>
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _attribute.GetValidationResult("80", null!));

    [TestMethod]
    [DataRow("2302:2305")]
    [DataRow("10011,30033,41144")]
    [DataRow("8080,8443")]
    [DataRow("21,60000:60100")]
    public void IsValid_RealWorldUFWExamples_ReturnsSuccess(string input)
    {
        // These are examples from the actual UFW rules provided in the requirements
        // Act
        ValidationResult? result = _attribute.GetValidationResult(input, _context);

        // Assert
        Assert.AreEqual(ValidationResult.Success, result);
    }
}