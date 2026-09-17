using Ufw.Client.Rules.Filtering.Text;

namespace Ufw.Client.Components.Rules.Filtering.Text;

internal sealed class TextRuleFilterDefinitionProvider : IRuleFilterDefinitionProvider
{
    public IReadOnlyList<RuleFilterDefinition> Definitions { get; } =
    [
        new(
            "text",
            "SearchRules",
            "FilterCategoryGeneral",
            typeof(TextRuleFilterEditor),
            static filter => filter is TextRuleFilter,
            static (filter, _, text) => $"{text["SearchRules"]}: {((TextRuleFilter)filter).Text}",
            Selectable: false),
    ];
}
