using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace UfwWebUI.Validation;

public sealed class ValidPortRangeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success; // Allow null/empty
        }

        string portString = value.ToString()!.Trim();
        
        // Split by comma for multiple port specifications
        string[] portSpecs = portString.Split(',');
        
        foreach (string spec in portSpecs)
        {
            string trimmedSpec = spec.Trim();
            
            // Check if it's a range (e.g., 80:82 or 60000:60100)
            if (trimmedSpec.Contains(':', StringComparison.Ordinal))
            {
                string[] rangeParts = trimmedSpec.Split(':');
                if (rangeParts.Length != 2)
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} contains an invalid port range: {trimmedSpec}");
                }

                if (!int.TryParse(rangeParts[0], out int startPort) || startPort < 0 || startPort > 65535)
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} contains an invalid start port in range: {trimmedSpec}");
                }

                if (!int.TryParse(rangeParts[1], out int endPort) || endPort < 0 || endPort > 65535)
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} contains an invalid end port in range: {trimmedSpec}");
                }

                if (startPort > endPort)
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} has a range where start port is greater than end port: {trimmedSpec}");
                }
            }
            else
            {
                // Single port
                if (!int.TryParse(trimmedSpec, out int port) || port < 0 || port > 65535)
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} contains an invalid port: {trimmedSpec}");
                }
            }
        }

        return ValidationResult.Success;
    }
}
