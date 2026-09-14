using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed partial class NetworkInterface(string? name = null) : RegexParserBase<NetworkInterface, IUfwListCommandResultRowVisitor>(name), IParser<NetworkInterface>, IRegexOwner
{
    public static NetworkInterface Instance { get; } = new();

    [GeneratedRegex(@"\Gon (?<interface>[A-Za-z][A-Za-z0-9._-]*)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new NetworkInterface(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string networkInterface = match.Groups["interface"].Value;
        syntaxNode = new NetworkInterfaceSyntaxNode(Name, networkInterface);
        return true;
    }
}
