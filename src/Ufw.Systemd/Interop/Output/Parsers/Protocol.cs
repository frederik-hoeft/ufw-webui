using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed partial class Protocol(string? name = null) : RegexParserBase<Protocol>(name), IParser<Protocol>, IRegexOwner
{
    public static Protocol Instance { get; } = new();

    [GeneratedRegex(@"\G/(?<protocol>tcp|udp)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new Protocol(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string protocolString = match.Groups["protocol"].Value;
        UfwProtocol? protocol = protocolString switch
        {
            "tcp" => UfwProtocol.Tcp,
            "udp" => UfwProtocol.Udp,
            _ => null,
        };
        if (protocol is null)
        {
            syntaxNode = null;
            return false;
        }
        syntaxNode = new ProtocolSyntaxNode(Name, protocol.Value);
        return true;
    }
}