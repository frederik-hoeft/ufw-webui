using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed partial class Ipv4Cidr(string? name = null) : RegexParserBase<Ipv4Cidr>(name), IParser<Ipv4Cidr>, IRegexOwner
{
    public static Ipv4Cidr Instance { get; } = new();

    [GeneratedRegex(@"\G(?<target>(?<ipv4>(0|[1-9][0-9]{0,2})(\.(0|[1-9][0-9]{0,2})){3})(/(?<cidr>0|[1-9][0-9]?))?)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new Ipv4Cidr(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string sourceAddress = match.Groups["target"].Value;
        ReadOnlySpan<char> sourceAddressSpan = sourceAddress.AsSpan();
        int slashIndex = sourceAddressSpan.IndexOf('/');
        ReadOnlySpan<char> ipv4Part;
        if (slashIndex != -1)
        {
            ipv4Part = sourceAddressSpan[..slashIndex];
            ReadOnlySpan<char> cidrPart = sourceAddressSpan[(slashIndex + 1)..];
            if (!int.TryParse(cidrPart, out int cidr) || cidr is < 0 or > 32)
            {
                syntaxNode = null;
                return false;
            }
        }
        else
        {
            ipv4Part = sourceAddressSpan;
        }
        foreach (Range range in ipv4Part.Split('.'))
        {
            ReadOnlySpan<char> octetSpan = ipv4Part[range];
            if (!int.TryParse(octetSpan, out int octet) || octet is < 0 or > 255)
            {
                syntaxNode = null;
                return false;
            }
        }
        syntaxNode = new Ipv4CidrSyntaxNode(Name, sourceAddress);
        return true;
    }
}