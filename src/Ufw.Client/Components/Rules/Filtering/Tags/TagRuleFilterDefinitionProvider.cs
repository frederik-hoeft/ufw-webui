using Ufw.Client.Rules.Filtering.Tags;

namespace Ufw.Client.Components.Rules.Filtering.Tags;

internal sealed class TagRuleFilterDefinitionProvider : IRuleFilterDefinitionProvider
{
    public IReadOnlyList<RuleFilterDefinition> Definitions { get; } =
    [
        new(
            "tag",
            "TagFilter",
            "FilterCategoryMetadata",
            typeof(TagRuleFilterEditor),
            static filter => filter is TagRuleFilter,
            static (filter, _, text) => $"{text["TagFilter"]}: {((TagRuleFilter)filter).Tag.Name}"),
    ];
}
