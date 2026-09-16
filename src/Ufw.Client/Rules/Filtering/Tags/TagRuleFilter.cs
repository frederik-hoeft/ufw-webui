using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Tags;

internal sealed record TagRuleFilter(string Tag) : RuleFilter;
