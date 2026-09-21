using Ufw.Web.Client.Features.Rules.Filtering.Protocols;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Protocols;

internal sealed class ProtocolRuleFilterDefinitionProvider : IRuleFilterDefinitionProvider
{
    public IReadOnlyList<RuleFilterDefinition> Definitions { get; } =
    [
        new(
            "protocol",
            "ProtocolFilter",
            "FilterCategoryTraffic",
            typeof(ProtocolRuleFilterEditor),
            static filter => filter is ProtocolRuleFilter,
            static (filter, ruleText, text) => $"{text["ProtocolColumn"]}: {ruleText.FormatProtocol(((ProtocolRuleFilter)filter).Protocol)}"),
    ];
}
