using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Filtering.Groups;

internal sealed record GroupRuleFilter(RuleGroup Group) : RuleFilter;
