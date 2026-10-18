using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed partial class CounterToken(string? name = null) : RegexParserBase<CounterToken, IIptablesTokenVisitor>(name), IParser<CounterToken>, IRegexOwner
{
    public static CounterToken Instance { get; } = new();

    [GeneratedRegex(@"\G\[[0-9]+:[0-9]+\]")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new CounterToken(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        syntaxNode = new IptablesTokenSyntaxNode(Name, IptablesTokenKind.Counter, match.Value);
        return true;
    }
}
