using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;
using Ufw.Client.Components.Rules;
using Ufw.Client.Errors;
using Ufw.Client.RuleInsertion;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Pages;

public sealed partial class CreateRule
{
    private readonly CancellationTokenSource _lifetime = new();
    private RulesPageState _state = RulesPageState.Initial;
    private FirewallRuleSpecification _draft = FirewallRuleDefaults.Create();
    private OrderedRuleInsertionNavigationContext? _orderedInsertionContext;
    private OrderedRuleInsertionContextError _orderedInsertionContextError;
    private RuleInsertionResponse? _insertionResult;
    private string _privateKey = string.Empty;
    private string? _reconciliationRuleIdentity;
    private bool _mutationMayHaveCompleted;
    private bool _orderedInsertionInvalidated;
    private bool _submitting;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["RulesBreadcrumb"], "/rules"),
        new BreadcrumbItem(RulesText["AddRuleBreadcrumb"], null, disabled: true),
    ];

    private bool HasLegacyInsertionTarget
        => !string.IsNullOrWhiteSpace(LegacyBeforeRuleId) || !string.IsNullOrWhiteSpace(LegacyAfterRuleId);

    private bool IsOrderedInsertionRequested
        => HasLegacyInsertionTarget
            || !string.IsNullOrWhiteSpace(InsertionBaselineFingerprint)
            || !string.IsNullOrWhiteSpace(InsertionAnchorValue)
            || !string.IsNullOrWhiteSpace(InsertionPlacementValue);

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

    protected async override Task OnInitializedAsync() => await LoadRulesAsync(RuleRefreshReason.Manual);

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

        return _state.Status == RulesPageStatus.Refreshing
            ? RulesText["AuthoritativeSnapshotRefreshing"]
            : RulesText["AuthoritativeSnapshotCurrent"];
    }

    private Task RefreshAsync()
    {
        if (_submitting || _state.IsLoading)
        {
            return Task.CompletedTask;
        }

        return LoadRulesAsync(RuleRefreshReason.Manual);
    }

    private async Task LoadRulesAsync(RuleRefreshReason reason)
    {
        if (_state.IsLoading)
        {
            return;
        }

        _state = _state.BeginRefresh(reason);
        try
        {
            RuleListResponse response = await RuleApiClient.GetRulesAsync(_lifetime.Token);
            _state = RulesPageState.CompleteRefresh(response);
            ResolveOrderedInsertionContext(response);

            if (_mutationMayHaveCompleted)
            {
                if (_reconciliationRuleIdentity is not null && ContainsRuleIdentity(_state.Snapshot!, _reconciliationRuleIdentity))
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
            _state = _state.FailRefresh(ClientErrors.Describe(exception));
        }
    }

    private void ResolveOrderedInsertionContext(RuleListResponse snapshot)
    {
        _orderedInsertionContext = null;
        _orderedInsertionContextError = OrderedRuleInsertionContextError.None;
        if (!IsOrderedInsertionRequested || _orderedInsertionInvalidated)
        {
            return;
        }
        if (HasLegacyInsertionTarget)
        {
            _orderedInsertionInvalidated = true;
            _orderedInsertionContextError = OrderedRuleInsertionContextError.Incomplete;
            return;
        }

        int? anchorOccurrenceId = int.TryParse(
            InsertionAnchorValue,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int parsedAnchor)
                ? parsedAnchor
                : null;
        if (!OrderedRuleInsertionNavigation.TryResolve(
            snapshot,
            InsertionBaselineFingerprint,
            anchorOccurrenceId,
            InsertionPlacementValue,
            out OrderedRuleInsertionNavigationContext? context,
            out OrderedRuleInsertionContextError error))
        {
            _orderedInsertionContextError = error;
            _orderedInsertionInvalidated = true;
            return;
        }

        _orderedInsertionContext = context;
        _draft.AddressFamily = context.AddressFamily;
    }

    private async Task SubmitRuleAsync()
    {
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
        _reconciliationRuleIdentity = RuleIdentity.Compute(normalized);
        _submitting = true;
        try
        {
            RuleMutationResponse mutation = await RuleMutations.AddRuleAsync(normalized, _privateKey, _lifetime.Token);
            _reconciliationRuleIdentity = GetRuleIdentity(mutation.Rule) ?? _reconciliationRuleIdentity;
            _mutationMayHaveCompleted = true;
            _submitting = false;
            await LoadRulesAsync(RuleRefreshReason.AfterMutation);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            ClientError error = ClientErrors.Describe(exception);
            _state = _state.AfterMutationFailure(error);
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

        RuleListResponse baseline = new(snapshot.FirewallActive, snapshot.Rules);
        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(_draft);
        _insertionResult = null;
        _submitting = true;
        try
        {
            RuleInsertionResponse response = await RuleMutations.InsertRuleAsync(
                baseline,
                context.AnchorOccurrenceId,
                context.Placement,
                normalized,
                _privateKey,
                _lifetime.Token);
            _insertionResult = response;
            _state = _state.AfterInsertion(response);

            if (response.Outcome == RuleInsertionOutcome.Completed)
            {
                Snackbar.Add(RulesText["OrderedInsertionApplied"], Severity.Success);
                Navigation.NavigateTo("/rules");
                return;
            }

            if (MustReselectInsertionAnchor(response, context))
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
            _state = _state.AfterMutationFailure(error);
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

        string position = context.Anchor.DisplayNumber is { } displayNumber
            ? RulesText["RulePosition", displayNumber.ToString("N0", CultureInfo.CurrentCulture)]
            : RulesText["SelectedRule"];
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

    private static bool MustReselectInsertionAnchor(
        RuleInsertionResponse response,
        OrderedRuleInsertionNavigationContext context)
    {
        if (response.Outcome is RuleInsertionOutcome.StaleBaseline or RuleInsertionOutcome.StateUncertain
            || response.FinalSnapshot is null)
        {
            return true;
        }

        return !string.Equals(
            FirewallRuleSnapshotFingerprint.Compute(response.FinalSnapshot),
            context.BaselineFingerprint,
            StringComparison.Ordinal);
    }

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

    private static string? GetRuleIdentity(ListedFirewallRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.RuleId))
        {
            return rule.RuleId;
        }

        return rule.Parsed && rule.Rule is not null ? RuleIdentity.Compute(rule.Rule) : null;
    }

    private static bool ContainsRuleIdentity(RuleSnapshot snapshot, string identity)
    {
        return snapshot.Rules.Any(rule => rule.Parsed
            && rule.Rule is not null
            && string.Equals(rule.RuleId ?? RuleIdentity.Compute(rule.Rule), identity, StringComparison.Ordinal));
    }
}
