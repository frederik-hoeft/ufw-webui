using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Shared.Firewall.Iptables.Parsing.Terminals;

internal sealed partial class UfwTupleProtocol(string? name = null) : RegexParserBase<UfwTupleProtocol, IUfwTupleVisitor>(name), IParser<UfwTupleProtocol>, IRegexOwner
{
    public static UfwTupleProtocol Instance { get; } = new();

    // UFW can store protocols that the mutable FirewallRuleSpecification cannot represent.
    [GeneratedRegex(@"\G(?<protocol>[a-zA-Z0-9-]+)(?=\s|$)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new UfwTupleProtocol(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        syntaxNode = new UfwTupleProtocolSyntaxNode(Name, match.Groups["protocol"].Value);
        return true;
    }
}
