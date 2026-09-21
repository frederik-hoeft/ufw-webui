using Ufw.Web.Client.UI.Components.Rules.Filtering.Actions;
using Ufw.Web.Client.UI.Components.Rules.Filtering.Directions;
using Ufw.Web.Client.UI.Components.Rules.Filtering.Networks;
using Ufw.Web.Client.UI.Components.Rules.Filtering.Ports;
using Ufw.Web.Client.UI.Components.Rules.Filtering.Protocols;
using Ufw.Web.Client.UI.Components.Rules.Filtering.Tags;
using Ufw.Web.Client.UI.Components.Rules.Filtering.Text;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering;

internal static class RuleFilterUiServiceCollectionExtensions
{
    public static IServiceCollection AddRuleFilterUiServices(this IServiceCollection services)
    {
        services.AddSingleton<IRuleFilterDefinitionProvider, NetworkRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, PortRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, ProtocolRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, ActionRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, DirectionRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, TagRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, TextRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterCatalog, RuleFilterCatalog>();
        return services;
    }
}
