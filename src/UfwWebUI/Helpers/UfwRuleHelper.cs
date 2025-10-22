namespace UfwWebUI.Helpers;

public static class UfwRuleHelper
{
    public static string NormalizeInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "any";
        }

        string normalized = input.Trim();

        // Normalize "any" to lowercase
        if (string.Equals(normalized, "any", StringComparison.OrdinalIgnoreCase))
        {
            return "any";
        }

        return normalized;
    }

    public static string NormalizePortRange(string? ports)
    {
        if (string.IsNullOrWhiteSpace(ports))
        {
            return string.Empty;
        }

        // Remove all whitespace from port ranges
        return ports.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
    }

    public static string GetDisplayValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "any", StringComparison.OrdinalIgnoreCase))
        {
            return "any";
        }

        return value;
    }

    public static bool IsAnyValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, "any", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetProtocolDisplay(string protocol)
    {
        return protocol.ToLowerInvariant();
    }
}
