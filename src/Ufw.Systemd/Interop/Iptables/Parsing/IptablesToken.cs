namespace Ufw.Systemd.Interop.Iptables.Parsing;

internal readonly record struct IptablesToken(IptablesTokenKind Kind, string Text, string? Role)
{
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
