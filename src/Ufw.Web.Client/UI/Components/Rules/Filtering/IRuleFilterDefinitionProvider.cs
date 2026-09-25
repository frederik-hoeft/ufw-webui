namespace Ufw.Web.Client.UI.Components.Rules.Filtering;

internal interface IRuleFilterDefinitionProvider
{
    IReadOnlyList<RuleFilterDefinition> Definitions { get; }
}
