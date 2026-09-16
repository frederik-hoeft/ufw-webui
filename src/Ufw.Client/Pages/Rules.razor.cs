using MudBlazor;
using Ufw.Client.Api;
using Ufw.Client.Components.Rules;
using Ufw.Client.Errors;
using Ufw.Client.RuleInsertion;
using Ufw.Client.RuleOrdering;
using Ufw.Client.Rules;
using Ufw.Client.Rules.Filtering;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Pages;

public sealed partial class Rules
{
    private static readonly DialogOptions s_deleteDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        DefaultFocus = DefaultFocus.FirstChild,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
    };

    private readonly CancellationTokenSource _lifetime = new();
    private RulesPageState _state = RulesPageState.Initial;
    private bool _deleteDialogOpen;
    private bool _deleting;
    private bool _reordering;
    private string _orderingPrivateKey = string.Empty;
    private RuleOrderingPreview? _orderingPreview;
    private RuleOrderingResultContext? _orderingResult;
    private RuleListProjection _ruleListProjection = RuleListProjection.Empty;
    private RuleFamilySelectionState _familySelection = RuleFamilySelectionState.Initial;
    private RuleQuery _ruleQuery = RuleQuery.Empty;
    private RuleFamilyQueryResult _ipv4QueryResult = new(FirewallAddressFamily.IPv4, [], 0);
    private RuleFamilyQueryResult _ipv6QueryResult = new(FirewallAddressFamily.IPv6, [], 0);

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["RulesBreadcrumb"], null, disabled: true),
    ];

    private bool HasOrderingPreview => _orderingPreview is not null;

    private RuleFamilyProjection IPv4Family => _ruleListProjection.GetFamily(FirewallAddressFamily.IPv4);

    private RuleFamilyProjection IPv6Family => _ruleListProjection.GetFamily(FirewallAddressFamily.IPv6);

    private IReadOnlyList<RuleRowProjection> IPv4VisibleRows => _ipv4QueryResult.VisibleRows;

    private IReadOnlyList<RuleRowProjection> IPv6VisibleRows => _ipv6QueryResult.VisibleRows;

    private RuleListInteractionState InteractionState => RuleListInteractionState.Resolve(_ruleQuery.IsActive, HasOrderingPreview);

    private bool IPv6FamilyAvailable =>
        _state.Snapshot is { } snapshot && RuleFamilySelectionState.IsIPv6Available(snapshot.Configuration.IPv6Enabled, IPv6Family.Rows.Count);

    private int SelectedFamilyTabIndex =>
        _familySelection.SelectedFamily == FirewallAddressFamily.IPv6 && IPv6FamilyAvailable ? 1 : 0;

    private bool IsBusy => _state.IsLoading || _deleting || _deleteDialogOpen || _reordering;

    private bool CanMutateFirewall => _state.IsCurrent && !_deleting && !_deleteDialogOpen && !_reordering && !HasOrderingPreview;

    private bool CanPreviewOrdering => _state.IsCurrent && !_deleting && !_deleteDialogOpen && !_reordering && InteractionState.CanOrder;

    private string RefreshButtonLabel => _state.Status switch
    {
        RulesPageStatus.Loading => RulesText["LoadingEllipsis"],
        RulesPageStatus.Refreshing => RulesText["RefreshingEllipsis"],
        _ => CommonText["Refresh"],
    };

    private string DescribeRuleCount(int count) => count == 1
        ? RulesText["RuleCountOne"]
        : RulesText["RuleCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];

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

    protected async override Task OnInitializedAsync() => await LoadRulesAsync(RuleRefreshReason.Manual);

    public void Dispose()
    {
        _orderingPrivateKey = string.Empty;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private Task RefreshAsync()
    {
        if (IsBusy)
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

        _orderingPreview = null;
        _orderingPrivateKey = string.Empty;
        _orderingResult = null;
        RefreshRuleListProjection();
        _state = _state.BeginRefresh(reason);
        try
        {
            Ufw.Shared.Ipc.Model.Responses.Domain.RuleListResponse response = await RuleApiClient.GetRulesAsync(_lifetime.Token);
            _state = RulesPageState.CompleteRefresh(response);
            RefreshRuleListProjection();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _state = _state.FailRefresh(ClientErrors.Describe(exception));
        }
    }

    private async Task BeginDeleteAsync(ListedFirewallRule rule)
    {
        if (!CanMutateFirewall || !rule.Parsed || rule.Rule is null || string.IsNullOrWhiteSpace(rule.RuleId))
        {
            return;
        }

        string? privateKey = null;
        _deleteDialogOpen = true;
        try
        {
            DialogParameters<DeleteRuleDialog> parameters = new();
            parameters.Add(component => component.Rule, rule);

            IDialogReference dialog = await DialogService.ShowAsync<DeleteRuleDialog>(RulesText["DeleteDialogTitle"], parameters, s_deleteDialogOptions);
            privateKey = await dialog.GetReturnValueAsync<string>();
        }
        finally
        {
            _deleteDialogOpen = false;
        }

        if (string.IsNullOrWhiteSpace(privateKey))
        {
            return;
        }

        try
        {
            if (_lifetime.IsCancellationRequested)
            {
                return;
            }

            if (!CanMutateFirewall)
            {
                Snackbar.Add(RulesText["DeleteStateChanged"], Severity.Warning);
                return;
            }

            await DeleteRuleAsync(rule, privateKey);
        }
        finally
        {
            privateKey = string.Empty;
        }
    }

    private async Task DeleteRuleAsync(ListedFirewallRule rule, string privateKey)
    {
        _deleting = true;
        try
        {
            await RuleMutations.DeleteRuleAsync(rule, privateKey, _lifetime.Token);
            _deleting = false;
            await LoadRulesAsync(RuleRefreshReason.AfterMutation);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            HandleMutationFailure(ClientErrors.Describe(exception));
        }
        finally
        {
            _deleting = false;
        }
    }

    private Task BeginOrderedInsertionAsync(RuleInsertionActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanMutateFirewall || _state.Snapshot is not { } snapshot)
        {
            return Task.CompletedTask;
        }

        if (!snapshot.Rules.Any(rule => ReferenceEquals(rule, request.Rule)))
        {
            Snackbar.Add(RulesText["InsertionTargetUnavailable"], Severity.Warning);
            return Task.CompletedTask;
        }
        if (request.Rule.Rule?.AddressFamily == FirewallAddressFamily.IPv6 && !snapshot.Configuration.IPv6Enabled)
        {
            Snackbar.Add(RulesText["InsertionIPv6Unavailable"], Severity.Warning);
            return Task.CompletedTask;
        }

        try
        {
            RuleListResponse baseline = new(snapshot.FirewallActive, snapshot.Rules, snapshot.Configuration);
            string uri = InsertionNavigation.BuildUri(baseline, request.Rule, request.Placement);
            Navigation.NavigateTo(uri);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            Snackbar.Add(exception.Message, Severity.Warning);
        }

        return Task.CompletedTask;
    }

    private Task MoveRuleAsync(RuleMoveRequest request)
    {
        if (!CanPreviewOrdering)
        {
            return Task.CompletedTask;
        }

        IReadOnlyList<ListedFirewallRule> authoritativeRules = _state.Snapshot?.Rules ?? [];
        try
        {
            RuleOrderingPreview preview = RuleOrderingProjection.Move(authoritativeRules, _orderingPreview, request);
            _orderingPreview = preview.HasChanges ? preview : null;
            _orderingResult = null;
            RefreshRuleListProjection();
            if (_orderingPreview is null)
            {
                _orderingPrivateKey = string.Empty;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            Snackbar.Add(exception.Message, Severity.Warning);
        }

        return Task.CompletedTask;
    }

    private async Task ApplyOrderingPreviewAsync()
    {
        if (_orderingPreview is null
            || _state.Snapshot is not { } snapshot
            || _reordering
            || string.IsNullOrWhiteSpace(_orderingPrivateKey))
        {
            return;
        }

        _reordering = true;
        try
        {
            RuleListResponse baseline = new(snapshot.FirewallActive, snapshot.Rules, snapshot.Configuration);
            int[] desiredOrder = _orderingPreview.DesiredOrder.ToArray();
            RuleReorderResponse response = await RuleOrdering.ApplyAsync(
                baseline,
                desiredOrder,
                _orderingPrivateKey,
                _lifetime.Token);

            _orderingPreview = null;
            _orderingResult = new RuleOrderingResultContext(response, baseline.Rules.ToArray(), desiredOrder);
            _state = _state.AfterReorder(response);
            RefreshRuleListProjection();
            if (response.Outcome == RuleReorderOutcome.Completed)
            {
                Snackbar.Add(RulesText["OrderingApplied"], Severity.Success);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            ClientError error = ClientErrors.Describe(exception);
            HandleMutationFailure(error);
            if (_state.IsStale)
            {
                _orderingPreview = null;
                RefreshRuleListProjection();
            }
        }
        finally
        {
            _orderingPrivateKey = string.Empty;
            _reordering = false;
        }
    }

    private void DiscardOrderingPreview()
    {
        if (!_reordering)
        {
            _orderingPreview = null;
            _orderingPrivateKey = string.Empty;
            RefreshRuleListProjection();
        }
    }

    private void SelectFamilyTab(int tabIndex)
    {
        FirewallAddressFamily requestedFamily = tabIndex == 1
            ? FirewallAddressFamily.IPv6
            : FirewallAddressFamily.IPv4;
        _familySelection = _familySelection.Select(requestedFamily, IPv6FamilyAvailable);
    }

    private void RefreshRuleListProjection()
    {
        _ruleListProjection = RuleListProjectionService.Create(_state.Snapshot?.Rules ?? [], _orderingPreview);
        RefreshRuleQueryProjection();
        _familySelection = _familySelection.Reconcile(IPv6FamilyAvailable);
    }

    private void RefreshRuleQueryProjection()
    {
        _ipv4QueryResult = RuleQueryService.Evaluate(IPv4Family, _ruleQuery);
        _ipv6QueryResult = RuleQueryService.Evaluate(IPv6Family, _ruleQuery);
    }

    private Task ChangeQueryAsync(RuleQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!InteractionState.CanChangeQuery)
        {
            return Task.CompletedTask;
        }

        _ruleQuery = query;
        RefreshRuleQueryProjection();
        return Task.CompletedTask;
    }

    private void HandleMutationFailure(ClientError error)
    {
        _state = _state.AfterMutationFailure(error);
        Snackbar.Add(error.Message, Severity.Error);
    }

    private sealed record RuleOrderingResultContext(RuleReorderResponse Response, IReadOnlyList<ListedFirewallRule> BaselineRules, IReadOnlyList<int> DesiredOrder);

    private string DescribeStaleState() => _state.StaleReason switch
    {
        RuleSnapshotStaleReason.MutationCommitted => RulesText["StaleMutationCommittedList"],
        RuleSnapshotStaleReason.MutationOutcomeUnknown => RulesText["StaleMutationUnknownList"],
        RuleSnapshotStaleReason.MutationRejectedRequiresRefresh => RulesText["StaleMutationRejectedList"],
        _ => RulesText["StaleRefreshFailedList"],
    };
}
