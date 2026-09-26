using Ufw.Web.Client.Features.Rules.Filtering.Groups;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Groups;

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
            static (filter, _, text) => $"{text["GroupFilter"]}: {((GroupRuleFilter)filter).Group.Name}"),
    ];
}
