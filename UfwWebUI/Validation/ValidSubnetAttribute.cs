using System.ComponentModel.DataAnnotations;

namespace UfwWebUI.Validation;

public class ValidSubnetAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success; // Allow null/empty - use [Required] if needed
        }

        var subnetString = value.ToString()!;
        
        // Subnet should be a number between 0 and 32 for IPv4
        if (int.TryParse(subnetString, out var subnet))
        {
            if (subnet >= 0 && subnet <= 32)
            {
                return ValidationResult.Success;
            }
        }

        return new ValidationResult($"The field {validationContext.DisplayName} must be a valid subnet mask (0-32).");
    }
}
