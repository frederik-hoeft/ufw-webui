using Ufw.Client.Rules.Filtering.Groups;

namespace Ufw.Client.Components.Rules.Filtering.Groups;

internal sealed class GroupRuleFilterDefinitionProvider : IRuleFilterDefinitionProvider
{
    public IReadOnlyList<RuleFilterDefinition> Definitions { get; } =
    [
        new(
            "group",
            "GroupFilter",
            "FilterCategoryMetadata",
            typeof(GroupRuleFilterEditor),
            static filter => filter is GroupRuleFilter,
            static (filter, _, text) => $"{text["GroupFilter"]}: {((GroupRuleFilter)filter).Group}"),
    ];
}
