using Ufw.Client.Rules.Filtering;
using Ufw.Client.Rules.Filtering.Ports;

namespace Ufw.Client.Components.Rules.Filtering.Ports;

internal sealed class PortRuleFilterDefinitionProvider : IRuleFilterDefinitionProvider
{
    public IReadOnlyList<RuleFilterDefinition> Definitions { get; } =
    [
        new(
            "port",
            "PortFilter",
            "FilterCategoryTraffic",
            typeof(PortRuleFilterEditor),
            static filter => filter is PortRuleFilter { Endpoint: RuleEndpointField.Any },
            static (filter, _, text) => $"{text["PortFilter"]}: {((PortRuleFilter)filter).Ports.CanonicalValue}"),
    ];
}
