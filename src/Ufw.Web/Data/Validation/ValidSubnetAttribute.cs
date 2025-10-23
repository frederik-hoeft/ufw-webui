using System.ComponentModel.DataAnnotations;

namespace Ufw.Web.Data.Validation;

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

        // Split by comma and validate each segment
        int start = 0;
        while (start < input.Length)
        {
            int commaIndex = input[start..].IndexOf(',');
            int mask = commaIndex >> 31; // -1 if commaIndex == -1, 0 otherwise
            //int segmentEnd = commaIndex == -1 ? input.Length : start + commaIndex;
            int segmentEnd = input.Length & mask | start + commaIndex & ~mask;
            ReadOnlySpan<char> portOrRange = input[start..segmentEnd].TrimEnd();

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
            // Move to next segment
            if (commaIndex == -1)
            {
                break;
            }
            start += commaIndex + 1;
        }

        return ValidationResult.Success;
    }
}
