using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Components.Rules.Filtering;

internal interface IRuleFilterCatalog
{
    IReadOnlyList<RuleFilterDefinition> Definitions { get; }

    RuleFilterDefinition Resolve(RuleFilter filter);
}
