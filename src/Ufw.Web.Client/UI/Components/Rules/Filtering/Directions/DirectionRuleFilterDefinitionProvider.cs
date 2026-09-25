using Ufw.Web.Client.Features.Rules.Filtering.Directions;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Directions;

internal sealed class DirectionRuleFilterDefinitionProvider : IRuleFilterDefinitionProvider
{
    public IReadOnlyList<RuleFilterDefinition> Definitions { get; } =
    [
        new(
            "direction",
            "DirectionFilter",
            "FilterCategoryRule",
            typeof(DirectionRuleFilterEditor),
            static filter => filter is DirectionRuleFilter,
            static (filter, ruleText, text) => $"{text["DirectionColumn"]}: {ruleText.FormatDirection(((DirectionRuleFilter)filter).Direction)}"),
    ];
}
