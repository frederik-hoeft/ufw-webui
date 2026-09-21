namespace Ufw.Web.Client.Components.Rules.Filtering;

internal interface IRuleFilterDefinitionProvider
{
    IReadOnlyList<RuleFilterDefinition> Definitions { get; }
}
