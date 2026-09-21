using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.Components.Rules.Filtering;

internal interface IRuleFilterCatalog
{
    IReadOnlyList<RuleFilterDefinition> Definitions { get; }

    RuleFilterDefinition Resolve(RuleFilter filter);
}
