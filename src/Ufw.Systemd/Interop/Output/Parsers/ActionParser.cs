using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed partial class RoutingAction(string? name = null) : RegexParserBase<RoutingAction>(name), IParser<RoutingAction>, IRegexOwner
{
    public static RoutingAction Instance { get; } = new();

    [GeneratedRegex(@"\G(?<action>ALLOW|DENY|REJECT|LIMIT) (?<forward>IN|FWD)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new RoutingAction(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string action = match.Groups["action"].Value;
        string forward = match.Groups["forward"].Value;
        RuleType? ruleType = action switch
        {
            "ALLOW" => RuleType.Allow,
            "DENY" => RuleType.Deny,
            "REJECT" => RuleType.Reject,
            "LIMIT" => RuleType.Limit,
            _ => null,
        };
        Direction? direction = forward switch
        {
            "IN" => Direction.In,
            "OUT" => Direction.Out,
            "FWD" => Direction.Forward,
            _ => null,
        };
        if (ruleType is null || direction is null)
        {
            syntaxNode = null;
            return false;
        }
        syntaxNode = new ActionSyntaxNode(Name, ruleType.Value, direction.Value);
        return true;
    }
}
