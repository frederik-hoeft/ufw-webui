using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;

namespace Ufw.Web.Data.Validation;

internal sealed class ValidIPv4AddressOrAnyAttribute : ValidationAttribute
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

        // Check if it's "any" or "0.0.0.0/0"
        if (input.Equals("any", StringComparison.OrdinalIgnoreCase) || input.Equals("0.0.0.0/0", StringComparison.Ordinal))
        {
            return ValidationResult.Success;
        }

        // Check if it contains a CIDR notation
        int cidrIndex = input.IndexOf('/');
        if (cidrIndex != -1)
        {
            int lastCidrIndex = input.LastIndexOf('/');
            if (cidrIndex != lastCidrIndex)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} must be a valid IPv4 address, optionally with CIDR notation (e.g., 192.168.1.0/24 or 'any'.");
            }
            // Validate IP part
            ReadOnlySpan<char> ipPart = input[..cidrIndex].TrimEnd();
            if (ipPart.Count('.') != 3 || !IPAddress.TryParse(ipPart, out IPAddress? ipAddress) || ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} must contain a valid IPv4 address.");
            }

            // Validate subnet part
            ReadOnlySpan<char> subnetPart = input[(cidrIndex + 1)..].TrimStart();
            if (!int.TryParse(subnetPart, out int subnet) || subnet is < 0 or > 32)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} must have a valid subnet mask (0-32).");
            }

            return ValidationResult.Success;
        }

        // Validate as plain IP address
        if (input.Count('.') == 3 && IPAddress.TryParse(input, out IPAddress? ip) && ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return ValidationResult.Success;
        }

        return new ValidationResult($"The field {validationContext.DisplayName} must be a valid IPv4 address, CIDR notation (e.g., 192.168.1.0/24), or 'any'.");
    }
}
