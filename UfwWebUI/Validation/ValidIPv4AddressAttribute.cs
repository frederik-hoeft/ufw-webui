using System.ComponentModel.DataAnnotations;
using System.Net;

namespace UfwWebUI.Validation;

public class ValidIPv4AddressAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success; // Allow null/empty - use [Required] if needed
        }

        var ipString = value.ToString()!;
        
        if (IPAddress.TryParse(ipString, out var ipAddress))
        {
            // Check if it's IPv4
            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return ValidationResult.Success;
            }
        }

        return new ValidationResult($"The field {validationContext.DisplayName} must be a valid IPv4 address.");
    }
}
