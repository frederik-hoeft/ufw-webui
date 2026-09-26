using Ufw.Shared.Firewall.Rendering;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Features.Rules.Filtering.Actions;
using Ufw.Web.Client.Features.Rules.Filtering.Directions;
using Ufw.Web.Client.Features.Rules.Filtering.Groups;
using Ufw.Web.Client.Features.Rules.Filtering.KnownHosts;
using Ufw.Web.Client.Features.Rules.Filtering.Networks;
using Ufw.Web.Client.Features.Rules.Filtering.Ports;
using Ufw.Web.Client.Features.Rules.Filtering.Protocols;
using Ufw.Web.Client.Features.Rules.Filtering.Tags;
using Ufw.Web.Client.Features.Rules.Filtering.Text;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Insertion;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Ordering;
using Ufw.Web.Client.Features.Rules.Presentation;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Features.Rules.Services;

internal static class RuleManagementServiceCollectionExtensions
{
    public static IServiceCollection AddRuleManagementServices(this IServiceCollection services)
    {
        services.AddSingleton<IUfwRuleCommandRenderer, UfwRuleCommandRenderer>();
        services.AddScoped<IFirewallRuleText, FirewallRuleText>();
        services.AddSingleton<IRuleEndpointKnownHostProjectionService, RuleEndpointKnownHostProjectionService>();
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
        services.AddScoped<IRuleGroupCatalogService, RuleGroupCatalogService>();
        services.AddScoped<IRuleGroupDeletionWorkflowService, RuleGroupDeletionWorkflowService>();
        services.AddSingleton<IRuleGroupManagementProjectionService, RuleGroupManagementProjectionService>();
        services.AddSingleton<IRuleTagColorGenerator, RuleTagColorGenerator>();
        services.AddScoped<IRuleMetadataReconciliationService, RuleMetadataReconciliationService>();
        services.AddSingleton<IRuleTagFilterReconciler, RuleTagFilterReconciler>();
        services.AddSingleton<IRuleGroupFilterReconciler, RuleGroupFilterReconciler>();
        services.AddSingleton<IRuleKnownHostProjectionService, RuleKnownHostProjectionService>();
        services.AddSingleton<IRuleFilterEvaluator, NetworkRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, PortRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, ProtocolRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, ActionRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, DirectionRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, TagRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, GroupRuleFilterEvaluator>();
        services.AddSingleton<IRuleFilterEvaluator, TextRuleFilterEvaluator>();
        services.AddSingleton<IRuleQueryService, RuleQueryService>();
        services.AddSingleton<IRuleInsertionNavigationService, RuleInsertionNavigationService>();
        services.AddSingleton<IRuleMutationReconciliationService, RuleMutationReconciliationService>();
        return services;
    }
}
