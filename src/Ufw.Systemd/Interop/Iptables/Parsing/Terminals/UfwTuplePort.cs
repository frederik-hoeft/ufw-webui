using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed partial class UfwTuplePort(string? name, UfwTupleFieldRole role) : RegexParserBase<UfwTuplePort, IUfwTupleVisitor>(name), IRegexOwner
{
    [GeneratedRegex(@"\G(?<value>[^\s]+)(?=\s|$)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new UfwTuplePort(name, role);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        syntaxNode = new UfwTuplePortSyntaxNode(Name, role, match.Groups["value"].Value);
        return true;
    }
}
