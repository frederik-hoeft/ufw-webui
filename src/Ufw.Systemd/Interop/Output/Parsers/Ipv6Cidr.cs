using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Ipv6Cidr(string? name = null) : IParser<Ipv6Cidr>
{
    public static Ipv6Cidr Instance { get; } = new();

    public string? Name => name;

    public IParser NamedCopy(string name) => new Ipv6Cidr(name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ReadOnlySpan<char> remaining = input.AsSpan(offset);
        int tokenLength = remaining.IndexOfAny(' ', '\t');
        if (tokenLength < 0)
        {
            tokenLength = remaining.Length;
        }

        ReadOnlySpan<char> token = remaining[..tokenLength];
        if (token.EndsWith("/tcp", StringComparison.Ordinal) || token.EndsWith("/udp", StringComparison.Ordinal))
        {
            token = token[..^4];
            tokenLength -= 4;
        }

        int slash = token.LastIndexOf('/');
        ReadOnlySpan<char> host = slash < 0 ? token : token[..slash];
        if (host.IndexOf(':') < 0
            || !IPAddress.TryParse(host, out IPAddress? address)
            || address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        if (slash >= 0
            && (!int.TryParse(token[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int prefix)
                || prefix is < 0 or > 128))
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        string value = token.ToString();
        syntaxNode = new Ipv6CidrSyntaxNode(Name, value);
        charsConsumed = tokenLength;
        return true;
    }
}
