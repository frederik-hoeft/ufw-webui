using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Shared.Firewall;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed partial class UfwTupleAction(string? name = null) : RegexParserBase<UfwTupleAction, IUfwTupleVisitor>(name), IParser<UfwTupleAction>, IRegexOwner
{
    public static UfwTupleAction Instance { get; } = new();

    [GeneratedRegex(@"\G(?<route>route:)?(?<action>allow|deny|reject|limit)(?:_(?<logging>log|log-all))?(?=\s|$)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new UfwTupleAction(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        FirewallAction action = match.Groups["action"].Value switch
        {
            "allow" => FirewallAction.Allow,
            "deny" => FirewallAction.Deny,
            "reject" => FirewallAction.Reject,
            _ => FirewallAction.Limit,
        };
        UfwTupleLogging logging = match.Groups["logging"].Value switch
        {
            "log" => UfwTupleLogging.Log,
            "log-all" => UfwTupleLogging.LogAll,
            _ => UfwTupleLogging.None,
        };
        syntaxNode = new UfwTupleActionSyntaxNode(Name, action, match.Groups["route"].Success, logging);
        return true;
    }
}
