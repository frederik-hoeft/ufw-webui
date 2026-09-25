using Ufw.Web.Client.Features.Rules.Filtering.Actions;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Actions;

internal sealed class ActionRuleFilterDefinitionProvider : IRuleFilterDefinitionProvider
{
    public IReadOnlyList<RuleFilterDefinition> Definitions { get; } =
    [
        new(
            "action",
            "ActionFilter",
            "FilterCategoryRule",
            typeof(ActionRuleFilterEditor),
            static filter => filter is ActionRuleFilter,
            static (filter, ruleText, text) => $"{text["ActionColumn"]}: {ruleText.FormatAction(((ActionRuleFilter)filter).Action)}"),
    ];
}
