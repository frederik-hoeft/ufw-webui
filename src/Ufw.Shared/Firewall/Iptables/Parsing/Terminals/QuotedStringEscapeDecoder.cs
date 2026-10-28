using System.Text;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

/// <summary>Decodes the backslash-escaped characters accepted by the quoted-token grammars.</summary>
internal static class QuotedStringEscapeDecoder
{
    public static string Decode(string content)
    {
        StringBuilder unescaped = new(content.Length);
        for (int index = 0; index < content.Length; index++)
        {
            if (content[index] == '\\' && index + 1 < content.Length)
            {
                index++;
            }

            unescaped.Append(content[index]);
        }

        return unescaped.ToString();
    }
}
