using Microsoft.AspNetCore.Components;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.UI.Components.Rules;

/// <summary>Workspace-owned rule actions. Row shells supply the current occurrence but do not interpret action semantics.</summary>
public sealed record RuleRowActionHandlers(
    EventCallback<RuleRowProjection> MoveToPositionRequested,
    EventCallback<RuleRowProjection> EditRequested,
    EventCallback<RuleRowProjection> MetadataEditRequested,
    EventCallback<RuleRowProjection> SaveAsTemplateRequested,
    EventCallback<RuleRowProjection> DisableRequested,
    EventCallback<ListedFirewallRule> DeleteRequested,
    EventCallback<RuleInsertionActionRequest> InsertionRequested);
