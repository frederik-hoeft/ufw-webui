using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.UI.Components.Rules.Metadata;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Client.Features.Rules.Insertion;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class CreateRule
{
    private readonly CancellationTokenSource _lifetime = new();
    private RuleInventoryState _state = RuleInventoryState.Initial;
    private FirewallRuleSpecification _draft = null!;
    private OrderedRuleInsertionNavigationContext? _orderedInsertionContext;
    private OrderedRuleInsertionContextError _orderedInsertionContextError;
    private RuleInsertionResponse? _insertionResult;
    private RuleMetadataEditor? _metadataEditor;
    private RuleMetadataEditorResult _metadataDraft = RuleMetadataEditorResult.Empty;
    private string _privateKey = string.Empty;
    private string? _reconciliationRuleIdentity;
    private bool _mutationMayHaveCompleted;
    private bool _orderedInsertionInvalidated;
    private bool _submitting;
    private bool _initialAddressFamilyApplied;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["RulesBreadcrumb"], "/rules"),
        new BreadcrumbItem(RulesText["AddRuleBreadcrumb"], null, disabled: true),
    ];

    private RuleInsertionNavigationQuery InsertionQuery => new(InsertionBaselineFingerprint, InsertionAnchorValue, InsertionPlacementValue, LegacyBeforeRuleId, LegacyAfterRuleId);

    private bool HasLegacyInsertionTarget => InsertionQuery.HasLegacyTarget;

    private bool IsOrderedInsertionRequested => InsertionQuery.IsRequested;

    private bool CanUseOrderedInsertionContext
        => !IsOrderedInsertionRequested || !_orderedInsertionInvalidated && _orderedInsertionContext is not null;

    private bool CanEdit => _state.IsCurrent && !_submitting && !_mutationMayHaveCompleted && CanUseOrderedInsertionContext;

    private bool CanSubmit => CanEdit;

    private string HeaderDescription => IsOrderedInsertionRequested
        ? RulesText["CreateOrderedDescription"]
        : RulesText["CreateDescription"];

    private string RuleDefinitionDescription => IsOrderedInsertionRequested
        ? RulesText["OrderedRuleDefinitionDescription"]
        : RulesText["DefinitionDescription"];

    [Parameter, SupplyParameterFromQuery(Name = "family")]
    public string? InitialAddressFamilyValue { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "baseline")]
    public string? InsertionBaselineFingerprint { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "anchor")]
    public string? InsertionAnchorValue { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "placement")]
    public string? InsertionPlacementValue { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "before")]
    public string? LegacyBeforeRuleId { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "after")]
    public string? LegacyAfterRuleId { get; set; }

    protected async override Task OnInitializedAsync()
    {
        _draft = RuleDraftFactory.Create();
        await LoadRulesAsync(RuleInventoryRefreshReason.Manual);
    }

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
        if (_submitting || _state.IsLoading)
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
            RuleInventoryResponse response = await RuleApiClient.GetInventoryAsync(_lifetime.Token);
            _state = _state.MoveNext(new RuleInventoryTransition.RefreshCompleted(response));
            ApplyInitialAddressFamily(response.Firewall.Configuration);
            ResolveOrderedInsertionContext(response.Firewall);

            if (_mutationMayHaveCompleted)
            {
                if (_reconciliationRuleIdentity is not null && MutationReconciliation.IsPresent(_state.Snapshot!, _reconciliationRuleIdentity))
                {
                    Navigation.NavigateTo("/rules");
                    return;
                }

                _mutationMayHaveCompleted = false;
                _reconciliationRuleIdentity = null;
                Snackbar.Add(RulesText["SubmittedRuleMissing"], Severity.Warning);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _state = _state.MoveNext(new RuleInventoryTransition.RefreshFailed(ClientErrors.Describe(exception)));
        }
    }

    private void ApplyInitialAddressFamily(FirewallConfigurationSnapshot configuration)
    {
        if (_initialAddressFamilyApplied)
        {
            return;
        }

        _initialAddressFamilyApplied = true;
        if (IsOrderedInsertionRequested)
        {
            return;
        }

        _draft.AddressFamily = InitialAddressFamilyValue?.Trim().ToUpperInvariant() switch
        {
            "IPV4" => FirewallAddressFamily.IPv4,
            "IPV6" when configuration.IPv6Enabled => FirewallAddressFamily.IPv6,
            _ => _draft.AddressFamily,
        };
    }

    private void ResolveOrderedInsertionContext(RuleListResponse snapshot)
    {
        _orderedInsertionContext = null;
        _orderedInsertionContextError = OrderedRuleInsertionContextError.None;
        if (!IsOrderedInsertionRequested || _orderedInsertionInvalidated)
        {
            return;
        }

        RuleInsertionNavigationResolution resolution = InsertionNavigation.Resolve(snapshot, InsertionQuery);
        if (!resolution.Succeeded)
        {
            _orderedInsertionContextError = resolution.Error;
            _orderedInsertionInvalidated = true;
            return;
        }

        _orderedInsertionContext = resolution.Context;
        _draft.AddressFamily = resolution.Context!.AddressFamily;
    }

    private async Task SubmitRuleAsync()
    {
        if (_metadataEditor is not null && !await _metadataEditor.ValidateAsync())
        {
            return;
        }

        _metadataDraft = _metadataDraft.Normalize();
        if (IsOrderedInsertionRequested)
        {
            await InsertRuleAsync();
            return;
        }

        await AddRuleAsync();
    }

    private async Task AddRuleAsync()
    {
        if (!CanSubmit || string.IsNullOrWhiteSpace(_privateKey))
        {
            return;
        }

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(_draft);
        // Keep the requested identity as the fallback for an uncertain outcome where no
        // mutation response is available. A successful family-neutral add returns a
        // concrete family-specific rule, so prefer that identity for the subsequent
        // authoritative refresh.
        _reconciliationRuleIdentity = MutationReconciliation.GetRequestedIdentity(normalized);
        _submitting = true;
        try
        {
            RuleMutationResponse mutation = await RuleMutations.AddRuleAsync(normalized, _privateKey, _lifetime.Token);
            _reconciliationRuleIdentity = MutationReconciliation.GetMutationIdentity(mutation, _reconciliationRuleIdentity);
            await SaveCreatedRuleMetadataAsync(mutation.Rule.RuleId);
            _mutationMayHaveCompleted = true;
            _submitting = false;
            await LoadRulesAsync(RuleInventoryRefreshReason.AfterMutation);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            ClientError error = ClientErrors.Describe(exception);
            _state = _state.MoveNext(new RuleInventoryTransition.MutationFailed(error));
            _mutationMayHaveCompleted = _state.StaleReason == RuleSnapshotStaleReason.MutationOutcomeUnknown;
            if (!_mutationMayHaveCompleted)
            {
                _reconciliationRuleIdentity = null;
            }
            Snackbar.Add(error.Message, Severity.Error);
        }
        finally
        {
            _privateKey = string.Empty;
            _submitting = false;
        }
    }

    private async Task InsertRuleAsync()
    {
        if (!CanSubmit
            || string.IsNullOrWhiteSpace(_privateKey)
            || _orderedInsertionContext is not { } context
            || _state.Snapshot is not { } snapshot)
        {
            return;
        }

        RuleListResponse baseline = new(snapshot.FirewallActive, snapshot.Rules, snapshot.Configuration);
        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(_draft);
        _insertionResult = null;
        _submitting = true;
        try
        {
            RuleInsertionResponse response = await RuleMutations.InsertRuleAsync(baseline, context.AnchorOccurrenceId, context.Placement, normalized, _privateKey, _lifetime.Token);
            _insertionResult = response;
            _state = _state.MoveNext(new RuleInventoryTransition.InsertionCompleted(response, TimeProvider.GetUtcNow()));

            if (response.Outcome == RuleInsertionOutcome.Completed)
            {
                await SaveCreatedRuleMetadataAsync(response.InsertedRule?.RuleId);
                Snackbar.Add(RulesText["OrderedInsertionApplied"], Severity.Success);
                Navigation.NavigateTo("/rules");
                return;
            }

            if (MutationReconciliation.MustReselectInsertionAnchor(response, context))
            {
                InvalidateOrderedInsertionContext();
            }

            Severity severity = response.Outcome == RuleInsertionOutcome.StateUncertain
                ? Severity.Error
                : Severity.Warning;
            Snackbar.Add(response.Diagnostic ?? DescribeInsertionResultTitle(response.Outcome), severity);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            ClientError error = ClientErrors.Describe(exception);
            _state = _state.MoveNext(new RuleInventoryTransition.MutationFailed(error));
            if (_state.IsStale)
            {
                InvalidateOrderedInsertionContext();
            }
            Snackbar.Add(error.Message, Severity.Error);
        }
        finally
        {
            _privateKey = string.Empty;
            _submitting = false;
        }
    }

    private Task MetadataChanged(RuleMetadataEditorResult value)
    {
        _metadataDraft = value;
        return Task.CompletedTask;
    }

    private async Task SaveCreatedRuleMetadataAsync(string? ruleId)
    {
        if (_metadataDraft.IsEmpty || string.IsNullOrWhiteSpace(ruleId))
        {
            return;
        }

        try
        {
            await RuleApiClient.UpdateMetadataAsync(ruleId, new UpdateRuleMetadataRequest
            {
                Notes = _metadataDraft.Notes,
                TagIds = _metadataDraft.TagIds,
                GroupId = _metadataDraft.GroupId,
            }, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            string diagnostic = ClientErrors.TryDescribe(exception, out ClientError error)
                ? error.Message
                : RulesText["MetadataSaveAfterCreateFailed"];
            Snackbar.Add(RulesText["MetadataSaveAfterCreateFailedWithReason", diagnostic], Severity.Warning);
        }
    }

    private void Cancel()
    {
        if (!_submitting)
        {
            _privateKey = string.Empty;
            Navigation.NavigateTo("/rules");
        }
    }

    private string DescribeInsertionTarget()
    {
        if (_orderedInsertionContext is not { } context)
        {
            return DescribeInsertionContextError();
        }

        string position = RulesText["RulePosition", context.AnchorFamilyPosition.ToString("N0", CultureInfo.CurrentCulture)];
        string placement = context.Placement == RuleInsertionPlacement.Before
            ? RulesText["Before"]
            : RulesText["After"];
        return RulesText["InsertTargetDescription", placement, position];
    }

    private string DescribeInsertionContextError()
    {
        if (HasLegacyInsertionTarget)
        {
            return RulesText["OrderedInsertionLegacyContext"];
        }

        return _orderedInsertionContextError switch
        {
            OrderedRuleInsertionContextError.StaleBaseline => RulesText["InsertionBaselineStale"],
            OrderedRuleInsertionContextError.AnchorUnavailable => RulesText["InsertionTargetMissing"],
            OrderedRuleInsertionContextError.CapabilityUnavailable => RulesText["InsertionIPv6Unavailable"],
            OrderedRuleInsertionContextError.InvalidFingerprint
                or OrderedRuleInsertionContextError.InvalidPlacement
                or OrderedRuleInsertionContextError.Incomplete => RulesText["InsertionContextInvalid"],
            _ => RulesText["InsertionTargetUnavailable"],
        };
    }

    private string DescribeInsertionFamily() => _orderedInsertionContext is { } context
        ? RulesText["InsertionFamilyLocked", context.AddressFamily.ToString()]
        : string.Empty;

    private Severity InsertionResultSeverity => _insertionResult?.Outcome switch
    {
        RuleInsertionOutcome.StaleBaseline or RuleInsertionOutcome.PreconditionFailed => Severity.Warning,
        RuleInsertionOutcome.StateUncertain => Severity.Error,
        _ => Severity.Info,
    };

    private string DescribeInsertionResultTitle() => _insertionResult is { } result
        ? DescribeInsertionResultTitle(result.Outcome)
        : RulesText["OrderedInsertionResult"];

    private string DescribeInsertionResultTitle(RuleInsertionOutcome outcome) => outcome switch
    {
        RuleInsertionOutcome.Completed => RulesText["OrderedInsertionResultCompleted"],
        RuleInsertionOutcome.StaleBaseline => RulesText["OrderedInsertionResultStale"],
        RuleInsertionOutcome.PreconditionFailed => RulesText["OrderedInsertionResultPrecondition"],
        RuleInsertionOutcome.StateUncertain => RulesText["OrderedInsertionResultUncertain"],
        _ => RulesText["OrderedInsertionResult"],
    };

    private void InvalidateOrderedInsertionContext()
    {
        _orderedInsertionContext = null;
        _orderedInsertionInvalidated = true;
    }

    private string DescribeStaleState() => _state.StaleReason switch
    {
        RuleSnapshotStaleReason.MutationCommitted => RulesText["RefreshAfterMutationFailed"],
        RuleSnapshotStaleReason.MutationOutcomeUnknown => RulesText["MutationOutcomeUnknown"],
        RuleSnapshotStaleReason.MutationRejectedRequiresRefresh => RulesText["MutationRejectedRefresh"],
        _ => RulesText["LatestRefreshFailed"],
    };
}
