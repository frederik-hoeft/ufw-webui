namespace Ufw.Client.Components.Rules.Filtering;

internal interface IRuleFilterDefinitionProvider
{
    IReadOnlyList<RuleFilterDefinition> Definitions { get; }
}
