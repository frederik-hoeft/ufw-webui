using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Filtering.Tags;

internal sealed record TagRuleMatchEvidence(RuleTag Tag) : RuleMatchEvidence;
