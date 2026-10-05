using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering;

internal sealed class RuleFilterCatalog : IRuleFilterCatalog
{
    public RuleFilterCatalog(IEnumerable<IRuleFilterDefinitionProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        RuleFilterDefinition[] definitions = [.. providers.SelectMany(static provider => provider.Definitions)];
        string? duplicateKey = definitions
            .GroupBy(static definition => definition.Key, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1)
            ?.Key;
        if (duplicateKey is not null)
        {
            throw new InvalidOperationException($"More than one rule filter UI definition uses key '{duplicateKey}'.");
        }
        if (definitions.Any(static definition => !typeof(IRuleFilterEditor).IsAssignableFrom(definition.EditorComponentType)))
        {
            throw new InvalidOperationException("Every rule filter UI definition must reference an IRuleFilterEditor component.");
        }

        Definitions = definitions;
    }

    public IReadOnlyList<RuleFilterDefinition> Definitions { get; }

    public RuleFilterDefinition Resolve(RuleFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        RuleFilterDefinition[] matches = [.. Definitions.Where(definition => definition.Matches(filter))];
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"No UI definition is registered for {filter.GetType().Name}."),
            _ => throw new InvalidOperationException($"More than one UI definition matches {filter.GetType().Name}."),
        };
    }
}
