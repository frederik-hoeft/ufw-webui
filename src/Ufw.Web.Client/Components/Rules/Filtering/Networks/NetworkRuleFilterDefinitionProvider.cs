using Ufw.Web.Client.Features.Rules.Filtering.Networks;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.Components.Rules.Filtering.Networks;

internal sealed class NetworkRuleFilterDefinitionProvider : IRuleFilterDefinitionProvider
{
    public IReadOnlyList<RuleFilterDefinition> Definitions { get; } =
    [
        new(
            "source-network",
            "SourceNetworkFilter",
            "FilterCategoryAddressing",
            typeof(NetworkRuleFilterEditor),
            static filter => filter is NetworkRuleFilter { Endpoint: RuleEndpointField.Source },
            static (filter, _, text) => $"{text["FromColumn"]}: {((NetworkRuleFilter)filter).Network.CanonicalValue}",
            EditorParameters: new Dictionary<string, object> { [nameof(NetworkRuleFilterEditor.SourceEndpoint)] = true }),
        new(
            "destination-network",
            "DestinationNetworkFilter",
            "FilterCategoryAddressing",
            typeof(NetworkRuleFilterEditor),
            static filter => filter is NetworkRuleFilter { Endpoint: RuleEndpointField.Destination },
            static (filter, _, text) => $"{text["ToColumn"]}: {((NetworkRuleFilter)filter).Network.CanonicalValue}",
            EditorParameters: new Dictionary<string, object> { [nameof(NetworkRuleFilterEditor.SourceEndpoint)] = false }),
    ];
}
