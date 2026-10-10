using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Replacement;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.UI.Components.Rules.Metadata;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class EditRule
{
    private readonly CancellationTokenSource _lifetime = new();
    private RuleInventoryState _state = RuleInventoryState.Initial;
    private RuleReplacementNavigationContext? _replacementContext;
    private RuleReplacementContextError _replacementContextError;
    private RuleEditWorkflowState _workflow = RuleEditWorkflowState.Initial;
    private FirewallRuleSpecification? _originalRule;
    private FirewallRuleSpecification? _draft;
    private RuleMetadataEditor? _metadataEditor;
    private RuleMetadataEditorResult _originalMetadataDraft = RuleMetadataEditorResult.Empty;
    private RuleMetadataEditorResult _metadataDraft = RuleMetadataEditorResult.Empty;
    private string _privateKey = string.Empty;
    private bool _authoringInitialized;
    private bool _submitting;
    private bool _metadataSaving;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["RulesBreadcrumb"], "/rules"),
        new BreadcrumbItem(RulesText["EditRuleBreadcrumb"], null, disabled: true),
    ];

    private RuleReplacementNavigationQuery ReplacementQuery => new(ReplacementBaselineFingerprint, ReplacementTargetOccurrenceValue, ReplacementOriginalRuleId);

    private bool CanUseReplacementContext => !_workflow.ContextInvalidated && _replacementContext is not null;

    private bool FirewallReplacementCompleted => _workflow.FirewallCompleted;

    private bool CanEdit => _state.IsCurrent && CanUseReplacementContext && !_submitting && !_metadataSaving && !FirewallReplacementCompleted;

    private bool CanSubmit => CanEdit;

    private bool CanRetryMetadataSave => _workflow.CanRetryMetadataSave && !_metadataSaving;

    private Severity ReplacementTargetSeverity => _replacementContext is null || _workflow.ContextInvalidated && !FirewallReplacementCompleted ? Severity.Warning : Severity.Info;

    private bool HasAuthoringState => _authoringInitialized && _draft is not null && _originalRule is not null;

    private FirewallRuleSpecification Draft => _draft ?? throw new InvalidOperationException("Rule authoring has not been initialized.");

    private FirewallRuleSpecification OriginalRule => _originalRule ?? throw new InvalidOperationException("Rule authoring has not been initialized.");

    [Parameter, SupplyParameterFromQuery(Name = "baseline")]
    public string? ReplacementBaselineFingerprint { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "target")]
    public string? ReplacementTargetOccurrenceValue { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "ruleId")]
    public string? ReplacementOriginalRuleId { get; set; }

    protected async override Task OnInitializedAsync() => await LoadRulesAsync(RuleInventoryRefreshReason.Manual);

    public void Dispose()
    {
        _privateKey = string.Empty;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private string DescribeRuleCount(int count) => count == 1
        ? RulesText["CurrentRuleCountOne"]
        : RulesText["CurrentRuleCountMany", count.ToString("N0", CultureInfo.CurrentCulture)];

    private string DescribeSnapshotStatus()
    {
        if (_state.IsStale)
        {
            return RulesText["AuthoritativeSnapshotStale"];
        }

        return _state.Status == RuleInventoryStatus.Refreshing
            ? RulesText["AuthoritativeSnapshotRefreshing"]
            : RulesText["AuthoritativeSnapshotCurrent"];
    }

    private Task RefreshAsync()
    {
        if (_submitting || _metadataSaving || _state.IsLoading)
        {
            return Task.CompletedTask;
        }

        return LoadRulesAsync(RuleInventoryRefreshReason.Manual);
    }

    private async Task LoadRulesAsync(RuleInventoryRefreshReason reason)
    {
        if (_state.IsLoading)
        {
            return;
        }

        _state = _state.MoveNext(new RuleInventoryTransition.RefreshStarted(reason));
        try
        {
            RuleSnapshot snapshot = await RuleInventory.GetAsync(_lifetime.Token);
            _state = _state.MoveNext(new RuleInventoryTransition.RefreshCompleted(snapshot));
            ResolveReplacementContext(RuleSnapshotFactory.ToFirewallResponse(snapshot));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            _state = _state.MoveNext(new RuleInventoryTransition.RefreshFailed(ClientErrors.Describe(exception)));
        }
    }

    private void ResolveReplacementContext(RuleListResponse snapshot)
    {
        if (_workflow.ContextInvalidated)
        {
            return;
        }

        RuleReplacementNavigationResolution resolution = ReplacementNavigation.Resolve(snapshot, ReplacementQuery);
        if (!resolution.Succeeded)
        {
            _replacementContext = null;
            _replacementContextError = resolution.Error;
            _workflow = _workflow.InvalidateContext();
            return;
        }

        _replacementContext = resolution.Context;
        _replacementContextError = RuleReplacementContextError.None;
        if (!_authoringInitialized)
        {
            InitializeAuthoringState();
        }
    }

    private void InitializeAuthoringState()
    {
        if (_replacementContext?.Target.Rule is not { } targetRule || _state.Snapshot is not { } snapshot)
        {
            throw new InvalidOperationException("A resolved rule replacement context must contain a parsed target rule and authoritative inventory snapshot.");
        }

        _originalRule = RuleDraftFactory.CreateFromExisting(targetRule);
        _draft = RuleDraftFactory.CreateFromExisting(targetRule);
        snapshot.Metadata.TryGetValue(_replacementContext.OriginalRuleId, out RuleMetadata? metadata);
        _originalMetadataDraft = RuleMetadataEditorResult.FromMetadata(metadata).Normalize();
        _metadataDraft = _originalMetadataDraft;
        _authoringInitialized = true;
    }

    private async Task SubmitRuleAsync()
    {
        if (_metadataEditor is not null && !await _metadataEditor.ValidateAsync())
        {
            return;
        }

        _metadataDraft = _metadataDraft.Normalize();
        await ReplaceRuleAsync();
    }

    private async Task ReplaceRuleAsync()
    {
        if (!CanSubmit
            || string.IsNullOrWhiteSpace(_privateKey)
            || _replacementContext is not { } context
            || _state.Snapshot is not { } snapshot
            || _draft is null)
        {
            return;
        }

        _workflow = RuleEditWorkflowState.Initial;
        _submitting = true;
        try
        {
            _workflow = await ReplacementWorkflow.ReplaceAsync(
                snapshot, context, _draft, OriginalMetadataChange(), CurrentMetadataChange(), _privateKey, _lifetime.Token);
            RuleReplacementMutationResponse response = _workflow.Replacement!;
            _state = _state.MoveNext(new RuleInventoryTransition.ReplacementCompleted(response.Firewall, TimeProvider.GetUtcNow()));

            if (response.Firewall.Outcome != RuleReplacementOutcome.Completed)
            {
                Snackbar.Add(response.Firewall.Diagnostic ?? DescribeReplacementResultTitle(response.Firewall.Outcome), ReplacementResultSeverity);
                return;
            }

            if (response.MetadataReconciliation == RuleReplacementMetadataReconciliationOutcome.Failed)
            {
                Snackbar.Add(RulesText["ReplacementMetadataReconciliationFailed"], Severity.Error);
                return;
            }

            if (_workflow.MetadataSaveDiagnostic is { } diagnostic)
            {
                Snackbar.Add(RulesText["MetadataSaveAfterReplacementFailedWithReason", diagnostic], Severity.Warning);
                return;
            }

            Snackbar.Add(RulesText["RuleReplacementApplied"], Severity.Success);
            Navigation.NavigateTo("/rules");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            ClientError error = ClientErrors.Describe(exception);
            _state = _state.MoveNext(new RuleInventoryTransition.MutationFailed(error));
            if (_state.IsStale)
            {
                _workflow = _workflow.InvalidateContext();
            }
            Snackbar.Add(error.Message, Severity.Error);
        }
        finally
        {
            _privateKey = string.Empty;
            _submitting = false;
        }
    }

    private async Task<bool> SaveEditedMetadataAsync()
    {
        _metadataSaving = true;
        try
        {
            _workflow = await ReplacementWorkflow.RetryMetadataAsync(_workflow, OriginalMetadataChange(), CurrentMetadataChange(), _lifetime.Token);
            if (_workflow.MetadataSaveDiagnostic is { } diagnostic)
            {
                Snackbar.Add(RulesText["MetadataSaveAfterReplacementFailedWithReason", diagnostic], Severity.Warning);
                return false;
            }
            return true;
        }
        finally
        {
            _metadataSaving = false;
        }
    }

    private RuleMetadataChange OriginalMetadataChange() => new(_originalMetadataDraft.Notes, _originalMetadataDraft.TagIds, _originalMetadataDraft.GroupId);

    private RuleMetadataChange CurrentMetadataChange() => new(_metadataDraft.Notes, _metadataDraft.TagIds, _metadataDraft.GroupId);

    private async Task RetryMetadataSaveAsync()
    {
        if (!CanRetryMetadataSave)
        {
            return;
        }

        bool saved = await SaveEditedMetadataAsync();
        if (saved)
        {
            Snackbar.Add(RulesText["RuleReplacementApplied"], Severity.Success);
            Navigation.NavigateTo("/rules");
        }
    }

    private Task MetadataChanged(RuleMetadataEditorResult value)
    {
        _metadataDraft = value;
        return Task.CompletedTask;
    }

    private void Cancel()
    {
        if (_submitting || _metadataSaving)
        {
            return;
        }

        _privateKey = string.Empty;
        Navigation.NavigateTo("/rules");
    }

    private string DescribeReplacementTarget()
    {
        if (_replacementContext is not { } context)
        {
            return DescribeReplacementContextError();
        }

        return RulesText[
            "ReplacementTargetDescription",
            context.AddressFamily.ToString(),
            context.TargetFamilyPosition.ToString("N0", CultureInfo.CurrentCulture)];
    }

    private string DescribeReplacementContextError() => _replacementContextError switch
    {
        RuleReplacementContextError.StaleBaseline => RulesText["ReplacementBaselineStale"],
        RuleReplacementContextError.TargetUnavailable => RulesText["ReplacementTargetMissing"],
        RuleReplacementContextError.TargetMismatch => RulesText["ReplacementTargetMismatch"],
        RuleReplacementContextError.DuplicateIdentity => RulesText["ReplacementDuplicateIdentity"],
        RuleReplacementContextError.CapabilityUnavailable => RulesText["ReplacementIPv6Unavailable"],
        RuleReplacementContextError.InvalidFingerprint or RuleReplacementContextError.Incomplete => RulesText["ReplacementContextInvalid"],
        _ => RulesText["ReplacementTargetUnavailable"],
    };

    private Severity ReplacementResultSeverity => _workflow.Replacement?.Firewall.Outcome switch
    {
        RuleReplacementOutcome.StaleBaseline or RuleReplacementOutcome.PreconditionFailed => Severity.Warning,
        RuleReplacementOutcome.PartiallyCompleted or RuleReplacementOutcome.StateUncertain => Severity.Error,
        _ => Severity.Info,
    };

    private string DescribeReplacementResultTitle() => _workflow.Replacement is { } result
        ? DescribeReplacementResultTitle(result.Firewall.Outcome)
        : RulesText["RuleReplacementResult"];

    private string DescribeReplacementResultTitle(RuleReplacementOutcome outcome) => outcome switch
    {
        RuleReplacementOutcome.Completed => RulesText["RuleReplacementResultCompleted"],
        RuleReplacementOutcome.StaleBaseline => RulesText["RuleReplacementResultStale"],
        RuleReplacementOutcome.PreconditionFailed => RulesText["RuleReplacementResultPrecondition"],
        RuleReplacementOutcome.PartiallyCompleted => RulesText["RuleReplacementResultPartial"],
        RuleReplacementOutcome.StateUncertain => RulesText["RuleReplacementResultUncertain"],
        _ => RulesText["RuleReplacementResult"],
    };

    private string? DescribeRecoveryOutcome() => _workflow.Replacement?.Firewall.RecoveryOutcome switch
    {
        RuleReplacementRecoveryOutcome.RestoredBaseline => RulesText["RuleReplacementRecoveryRestored"].Value,
        RuleReplacementRecoveryOutcome.Failed => RulesText["RuleReplacementRecoveryFailed"].Value,
        _ => null,
    };

    private string DescribeStaleState() => _state.StaleReason switch
    {
        RuleSnapshotStaleReason.MutationCommitted => RulesText["RefreshAfterMutationFailed"],
        RuleSnapshotStaleReason.MutationOutcomeUnknown => RulesText["MutationOutcomeUnknown"],
        RuleSnapshotStaleReason.MutationRejectedRequiresRefresh => RulesText["MutationRejectedRefresh"],
        _ => RulesText["LatestRefreshFailed"],
    };
}
