using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Groups;

internal sealed record GroupRuleFilter(string Group) : RuleFilter;
