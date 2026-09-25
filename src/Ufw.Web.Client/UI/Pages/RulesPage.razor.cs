using MudBlazor;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.UI.Components.Rules.Metadata;
using Ufw.Web.Client.UI.Components.Rules;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Ordering;
using Ufw.Web.Client.Features.Rules.Services;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class RulesPage
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

    private static readonly DialogOptions s_metadataDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
    };

    private readonly CancellationTokenSource _lifetime = new();
    private RuleInventoryState _state = RuleInventoryState.Initial;
    private RulesPageInteractionState _pageInteraction = RulesPageInteractionState.Initial;
    private string _orderingPrivateKey = string.Empty;
    private RuleOrderingPreview? _orderingPreview;
    private RuleOrderingResultContext? _orderingResult;
    private RulesPageProjection _projection = RulesPageProjection.Empty;
    private RuleFamilySelectionState _familySelection = RuleFamilySelectionState.Initial;
    private RuleQuery _ruleQuery = RuleQuery.Empty;
    private IReadOnlyList<KnownHostInventoryItem> _knownHosts = [];

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["RulesBreadcrumb"], null, disabled: true),
    ];

    private bool HasOrderingPreview => _orderingPreview is not null;

    private RuleFamilyProjection IPv4Family => _projection.IPv4Family;

    private RuleFamilyProjection IPv6Family => _projection.IPv6Family;

    private IReadOnlyList<RuleQueryRow> IPv4QueryRows => _projection.IPv4Query.Rows;

    private IReadOnlyList<RuleQueryRow> IPv6QueryRows => _projection.IPv6Query.Rows;

    private RuleListInteractionState InteractionState => RuleListInteractionState.Resolve(_ruleQuery.IsActive, HasOrderingPreview);

    private bool IPv6FamilyAvailable => _projection.IPv6Available;

    private string CreateRuleHref => _familySelection.SelectedFamily == FirewallAddressFamily.IPv6
        ? "/rules/create?family=ipv6"
        : "/rules/create?family=ipv4";

    private int SelectedFamilyTabIndex =>
        _familySelection.SelectedFamily == FirewallAddressFamily.IPv6 && IPv6FamilyAvailable ? 1 : 0;

    private bool IsBusy => _state.IsLoading || _pageInteraction.IsBusy;

    private bool CanMutateFirewall => _state.IsCurrent && _pageInteraction.CanMutateFirewall && !HasOrderingPreview;

    private bool CanEditMetadata => _state.IsCurrent && _pageInteraction.CanEditMetadata;

    private bool CanPreviewOrdering => _state.IsCurrent && _pageInteraction.CanPreviewOrdering && InteractionState.CanOrder;

    private string RefreshButtonLabel => _state.Status switch
    {
        RuleInventoryStatus.Loading => RulesText["LoadingEllipsis"],
        RuleInventoryStatus.Refreshing => RulesText["RefreshingEllipsis"],
        _ => CommonText["Refresh"],
    };

    private string DescribeRuleCount(int count) => count == 1
        ? RulesText["RuleCountOne"]
        : RulesText["RuleCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];

    private string DescribeSnapshotCapturedAt() => _state.Snapshot is RuleSnapshot snapshot
        ? RulesText["SnapshotCapturedAt", FormatLocalDateTime(snapshot.CapturedAt)]
        : string.Empty;

    private static string FormatLocalDateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);

    protected async override Task OnInitializedAsync() => await LoadRulesAsync(RuleInventoryRefreshReason.Manual);

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

        return LoadRulesAsync(RuleInventoryRefreshReason.Manual);
    }

    private async Task LoadRulesAsync(RuleInventoryRefreshReason reason)
    {
        if (_state.IsLoading)
        {
            return;
        }

        _orderingPreview = null;
        _orderingPrivateKey = string.Empty;
        _orderingResult = null;
        RefreshRuleListProjection();
        _state = _state.MoveNext(new RuleInventoryTransition.RefreshStarted(reason));
        try
        {
            RuleInventoryResponse response = await RuleApiClient.GetInventoryAsync(_lifetime.Token);
            _state = _state.MoveNext(new RuleInventoryTransition.RefreshCompleted(response));
            await RefreshKnownHostsAsync();
            RefreshRuleListProjection();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _state = _state.MoveNext(new RuleInventoryTransition.RefreshFailed(ClientErrors.Describe(exception)));
        }
    }

    private async Task RefreshKnownHostsAsync()
    {
        try
        {
            KnownHostInventoryResponse response = await KnownHosts.RefreshAsync(_lifetime.Token);
            _knownHosts = response.Hosts.Where(static host => host.IsVisible).ToArray();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _knownHosts = KnownHosts.Current?.Hosts.Where(static host => host.IsVisible).ToArray() ?? [];
        }
    }

    private void KnownHostsChanged(KnownHostInventoryResponse response)
    {
        _knownHosts = response.Hosts.Where(static host => host.IsVisible).ToArray();
        RefreshRuleListProjection();
    }

    private async Task EditMetadataAsync(RuleRowProjection row)
    {
        string? ruleId = row.Rule.RuleId;
        if (!CanEditMetadata || string.IsNullOrWhiteSpace(ruleId))
        {
            return;
        }

        _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.MetadataDialogOpened());
        try
        {
            DialogParameters<EditRuleMetadataDialog> parameters = [];
            parameters.Add(component => component.Metadata, row.Metadata);
            IDialogReference dialog = await DialogService.ShowAsync<EditRuleMetadataDialog>(RulesText["EditMetadata"], parameters, s_metadataDialogOptions);
            RuleMetadataEditorResult? result = await dialog.GetReturnValueAsync<RuleMetadataEditorResult>();
            if (result is not null)
            {
                await SaveMetadataAsync(ruleId, result);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            Snackbar.Add(ClientErrors.Describe(exception).Message, Severity.Error);
        }
        finally
        {
            if (_pageInteraction.Mode == RulesPageInteractionMode.MetadataDialog)
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.MetadataDialogClosed());
            }
        }
    }

    private async Task SaveMetadataAsync(string ruleId, RuleMetadataEditorResult result)
    {
        _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.MetadataSaveStarted());
        try
        {
            RuleMetadataMutationResponse response = await RuleApiClient.UpdateMetadataAsync(ruleId, new UpdateRuleMetadataRequest
            {
                Notes = result.Notes,
                TagIds = result.TagIds,
            }, _lifetime.Token);
            _state = _state.MoveNext(new RuleInventoryTransition.MetadataMutationCompleted(ruleId, response));
            RefreshRuleListProjection();
            Snackbar.Add(RulesText["MetadataSaved"], Severity.Success);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            Snackbar.Add(ClientErrors.Describe(exception).Message, Severity.Error);
        }
        finally
        {
            _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.MetadataSaveCompleted());
        }
    }

    private async Task BeginDeleteAsync(ListedFirewallRule rule)
    {
        if (!CanMutateFirewall || !rule.Parsed || rule.Rule is null || string.IsNullOrWhiteSpace(rule.RuleId))
        {
            return;
        }

        string? privateKey = null;
        _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteDialogOpened());
        try
        {
            DialogParameters<DeleteRuleDialog> parameters = new()
            {
                { component => component.Rule, rule }
            };

            IDialogReference dialog = await DialogService.ShowAsync<DeleteRuleDialog>(RulesText["DeleteDialogTitle"], parameters, s_deleteDialogOptions);
            privateKey = await dialog.GetReturnValueAsync<string>();
        }
        finally
        {
            if (string.IsNullOrWhiteSpace(privateKey))
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteDialogClosed());
            }
        }

        if (string.IsNullOrWhiteSpace(privateKey))
        {
            return;
        }

        try
        {
            if (_lifetime.IsCancellationRequested)
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteDialogClosed());
                return;
            }

            _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteConfirmed());
            await DeleteRuleAsync(rule, privateKey);
        }
        finally
        {
            privateKey = string.Empty;
        }
    }

    private async Task DeleteRuleAsync(ListedFirewallRule rule, string privateKey)
    {
        try
        {
            await RuleMutations.DeleteRuleAsync(rule, privateKey, _lifetime.Token);
            _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteCompleted());
            await LoadRulesAsync(RuleInventoryRefreshReason.AfterMutation);
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
            if (_pageInteraction.Mode == RulesPageInteractionMode.Deleting)
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteCompleted());
            }
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
            || !_pageInteraction.CanPreviewOrdering
            || string.IsNullOrWhiteSpace(_orderingPrivateKey))
        {
            return;
        }

        _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.ReorderStarted());
        try
        {
            RuleListResponse baseline = new(snapshot.FirewallActive, snapshot.Rules, snapshot.Configuration);
            int[] desiredOrder = [.. _orderingPreview.DesiredOrder];
            RuleReorderResponse response = await RuleOrdering.ApplyAsync(baseline, desiredOrder, _orderingPrivateKey, _lifetime.Token);

            _orderingPreview = null;
            _orderingResult = new RuleOrderingResultContext(response, baseline.Rules.ToArray(), desiredOrder);
            _state = _state.MoveNext(new RuleInventoryTransition.ReorderCompleted(response, TimeProvider.GetUtcNow()));
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
            _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.ReorderCompleted());
        }
    }

    private void DiscardOrderingPreview()
    {
        if (!_pageInteraction.IsReordering)
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
        _projection = RulesPageProjectionService.Create(_state.Snapshot, _orderingPreview, _ruleQuery, _knownHosts);
        _familySelection = _familySelection.Reconcile(IPv6FamilyAvailable);
    }

    private Task ChangeQueryAsync(RuleQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!InteractionState.CanChangeQuery)
        {
            return Task.CompletedTask;
        }

        _ruleQuery = query;
        RefreshRuleListProjection();
        return Task.CompletedTask;
    }

    private void HandleMutationFailure(ClientError error)
    {
        _state = _state.MoveNext(new RuleInventoryTransition.MutationFailed(error));
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
