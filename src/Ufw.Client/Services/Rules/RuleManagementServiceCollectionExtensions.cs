using Ufw.Client.Api;
using Ufw.Client.Components.Rules.Filtering;
using Ufw.Client.Components.Rules.Filtering.Actions;
using Ufw.Client.Components.Rules.Filtering.Directions;
using Ufw.Client.Components.Rules.Filtering.Networks;
using Ufw.Client.Components.Rules.Filtering.Ports;
using Ufw.Client.Components.Rules.Filtering.Protocols;
using Ufw.Client.Components.Rules.Filtering.Tags;
using Ufw.Client.Components.Rules.Filtering.Text;
using Ufw.Client.RuleInsertion;
using Ufw.Client.RuleOrdering;
using Ufw.Client.Rules;
using Ufw.Client.Rules.Authoring;
using Ufw.Client.Rules.Filtering;
using Ufw.Client.Rules.Filtering.Actions;
using Ufw.Client.Rules.Filtering.Directions;
using Ufw.Client.Rules.Filtering.KnownHosts;
using Ufw.Client.Rules.Filtering.Networks;
using Ufw.Client.Rules.Filtering.Ports;
using Ufw.Client.Rules.Filtering.Protocols;
using Ufw.Client.Rules.Filtering.Tags;
using Ufw.Client.Rules.Filtering.Text;
using Ufw.Client.Rules.Metadata;
using Ufw.Client.Rules.Presentation;
using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Client.Services.Rules;

internal static class RuleManagementServiceCollectionExtensions
{
    public static IServiceCollection AddRuleManagementServices(this IServiceCollection services)
    {
        services.AddSingleton<IUfwRuleCommandRenderer, UfwRuleCommandRenderer>();
        services.AddScoped<IFirewallRuleText, FirewallRuleText>();
        services.AddScoped<IRuleValidationMessageLocalizer, RuleValidationMessageLocalizer>();
        services.AddScoped<IRuleEditorValidationService, RuleEditorValidationService>();
        services.AddScoped<IRuleEditorReferenceDataService, RuleEditorReferenceDataService>();
        services.AddSingleton<IRuleDraftFactory, RuleDraftFactory>();
        services.AddScoped<IRuleMutationService, RuleMutationService>();
        services.AddScoped<IRuleOrderingService, RuleOrderingService>();
        services.AddScoped<IRuleOrderingProjectionService, RuleOrderingProjectionService>();
        services.AddSingleton<IRuleOrderingResultProjectionService, RuleOrderingResultProjectionService>();
        services.AddSingleton<IRuleListProjectionService, RuleListProjectionService>();
        services.AddSingleton<IRulesPageProjectionService, RulesPageProjectionService>();
        services.AddScoped<IRuleTagCatalogService, RuleTagCatalogService>();
        services.AddSingleton<IRuleTagColorGenerator, RuleTagColorGenerator>();
        services.AddScoped<IRuleMetadataReconciliationService, RuleMetadataReconciliationService>();
        services.AddSingleton<IRuleTagFilterReconciler, RuleTagFilterReconciler>();
        services.AddSingleton<IRuleFilterDefinitionProvider, NetworkRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, PortRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, ProtocolRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, ActionRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, DirectionRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, TagRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterDefinitionProvider, TextRuleFilterDefinitionProvider>();
        services.AddSingleton<IRuleFilterCatalog, RuleFilterCatalog>();
        services.AddSingleton<IRuleKnownHostProjectionService, RuleKnownHostProjectionService>();
        services.AddSingleton<IRuleFilterEvaluator, NetworkRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, PortRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, ProtocolRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, ActionRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, DirectionRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, TagRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, TextRuleFilterEvaluator>();
        services.AddSingleton<IRuleQueryService, RuleQueryService>();
        services.AddSingleton<IRuleInsertionNavigationService, RuleInsertionNavigationService>();
        services.AddSingleton<IRuleMutationReconciliationService, RuleMutationReconciliationService>();
        return services;
    }
}
