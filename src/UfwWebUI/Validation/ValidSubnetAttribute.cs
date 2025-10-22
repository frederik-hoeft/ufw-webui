using System.ComponentModel.DataAnnotations;

namespace UfwWebUI.Validation;

internal sealed class ValidPortRangeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        string? rawInput = null;
        if (value is string s)
        {
            rawInput = s;
        }
        else if (value is not null)
        {
            rawInput = value.ToString();
        }
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return ValidationResult.Success; // Allow null/empty
        }

        ReadOnlySpan<char> input = rawInput.AsSpan().Trim();

        for (int start = 0, end = input[start..].IndexOf(','); start < input.Length && end != -1; start = end + 1)
        {
            ReadOnlySpan<char> portOrRange = input[start..end].Trim();

            // Check if it's a range (e.g., 80:82 or 60000:60100)
            int rangeIndex = portOrRange.IndexOf(':');
            if (rangeIndex != -1)
            {
                int lastRangeIndex = portOrRange.LastIndexOf(':');
                if (rangeIndex != lastRangeIndex)
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} contains an invalid port range: {portOrRange}");
                }
                ReadOnlySpan<char> startPortStr = portOrRange[..rangeIndex].TrimEnd();
                ReadOnlySpan<char> endPortStr = portOrRange[(rangeIndex + 1)..].TrimStart();
                if (!int.TryParse(startPortStr, out int startPort) || startPort is < 0 or > 65535)
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} contains an invalid start port in range: {portOrRange}");
                }
                if (!int.TryParse(endPortStr, out int endPort) || endPort is < 0 or > 65535)
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} contains an invalid end port in range: {portOrRange}");
                }
                if (startPort > endPort)
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} has a range where start port is greater than end port: {portOrRange}");
                }
            }
            else if (!int.TryParse(portOrRange, out int port) || port is < 0 or > 65535)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} contains an invalid port: {portOrRange}");
            }
        }

        return ValidationResult.Success;
    }
}
