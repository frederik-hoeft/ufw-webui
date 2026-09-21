using Ufw.Web.Client.Features.Rules.Filtering.Ports;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.Components.Rules.Filtering.Ports;

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
