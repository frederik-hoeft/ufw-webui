using System.ComponentModel.DataAnnotations;
using System.Net;

namespace UfwWebUI.Validation;

public class ValidIPv4AddressOrAnyAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success; // Allow null/empty
        }

        var input = value.ToString()!.Trim();
        
        // Check if it's "any" or "0.0.0.0/0"
        if (input.Equals("any", StringComparison.OrdinalIgnoreCase) || 
            input.Equals("0.0.0.0/0", StringComparison.Ordinal))
        {
            return ValidationResult.Success;
        }

        // Check if it contains a CIDR notation
        if (input.Contains('/'))
        {
            var parts = input.Split('/');
            if (parts.Length != 2)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} must be a valid IPv4 address with CIDR notation (e.g., 192.168.1.0/24) or 'any'.");
            }

            // Validate IP part
            if (!IPAddress.TryParse(parts[0], out var ipAddress) || 
                ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} must contain a valid IPv4 address.");
            }

            // Validate subnet part
            if (!int.TryParse(parts[1], out var subnet) || subnet < 0 || subnet > 32)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} must have a valid subnet mask (0-32).");
            }

            return ValidationResult.Success;
        }

        // Validate as plain IP address
        if (IPAddress.TryParse(input, out var ip) && 
            ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return ValidationResult.Success;
        }

        return new ValidationResult($"The field {validationContext.DisplayName} must be a valid IPv4 address, CIDR notation (e.g., 192.168.1.0/24), or 'any'.");
    }
}
