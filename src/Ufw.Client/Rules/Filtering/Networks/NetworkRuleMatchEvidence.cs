using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Networks;

internal sealed record NetworkRuleMatchEvidence(
    RuleEndpointField Endpoint,
    string RuleNetwork,
    string QueryNetwork,
    NetworkRuleMatchEvidence.RelationshipKind Relationship) : RuleMatchEvidence
{
    internal enum RelationshipKind
    {
        Equal,
        ContainsQuery,
        ContainedByQuery,
    }
}
