namespace Ufw.Shared.Firewall.Iptables.Parsing;

/// <summary>Decoded iptables lexical token with its category and optional grammar-assigned role.</summary>
/// <param name="Kind">Lexical category.</param>
/// <param name="Text">Decoded token text, without surrounding quotation marks.</param>
/// <param name="Role">Optional grammar-specific field role.</param>
public readonly record struct IptablesToken(IptablesTokenKind Kind, string Text, string? Role)
{
    /// <summary>Formats this token for a rule statement, quoting and escaping string values as needed.</summary>
    public string Format()
    {
        if (Kind != IptablesTokenKind.QuotedString)
        {
            return Text;
        }

        if (Text.Length == 0 || Text.IndexOfAny([' ', '\t', '"', '\'']) >= 0)
        {
            return "\"" + Text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        return Text;
    }
}
