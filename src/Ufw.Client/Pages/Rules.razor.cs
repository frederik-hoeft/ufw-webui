using MudBlazor;
using Ufw.Client.Api;
using Ufw.Client.Components.Rules;
using Ufw.Client.Errors;
using Ufw.Client.RuleOrdering;
using Ufw.Shared.Firewall;

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
    private RuleOrderingPreview? _orderingPreview;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["RulesBreadcrumb"], null, disabled: true),
    ];

    private bool HasOrderingPreview => _orderingPreview is not null;

    private IReadOnlyList<ListedFirewallRule> DisplayedRules
        => _orderingPreview?.Rules ?? _state.Snapshot?.Rules ?? [];

    private bool IsBusy => _state.IsLoading || _deleting || _deleteDialogOpen || _reordering;

    private bool CanMutateFirewall => _state.IsCurrent && !_deleting && !_deleteDialogOpen && !_reordering && !HasOrderingPreview;

    private bool CanPreviewOrdering => _state.IsCurrent && !_deleting && !_deleteDialogOpen && !_reordering;

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
        _state = _state.BeginRefresh(reason);
        try
        {
            Ufw.Shared.Ipc.Model.Responses.Domain.RuleListResponse response = await UfwApiClient.GetRulesAsync(_lifetime.Token);
            _state = RulesPageState.CompleteRefresh(response);
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
            await UfwApiClient.DeleteRuleAsync(rule, privateKey, _lifetime.Token);
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

    private Task MoveRuleAsync(RuleMoveRequest request)
    {
        if (!CanPreviewOrdering || string.IsNullOrWhiteSpace(request.RuleId))
        {
            return Task.CompletedTask;
        }

        IReadOnlyList<ListedFirewallRule> authoritativeRules = _state.Snapshot?.Rules ?? [];
        if (request.TargetPosition < 1 || request.TargetPosition > DisplayedRules.Count)
        {
            Snackbar.Add(RulesText["PositionOutside"], Severity.Warning);
            return Task.CompletedTask;
        }

        try
        {
            _orderingPreview = RuleOrderingProjection.Move(authoritativeRules, _orderingPreview, request);
        }
        catch (InvalidOperationException exception)
        {
            Snackbar.Add(exception.Message, Severity.Warning);
        }

        return Task.CompletedTask;
    }

    private async Task ApplyOrderingPreviewAsync()
    {
        if (_orderingPreview is null || _reordering)
        {
            return;
        }

        _reordering = true;
        try
        {
            await RuleOrderingApiClient.ApplyAsync(new RuleOrderingApplyRequest(_orderingPreview.Moves), _lifetime.Token);

            _reordering = false;
            await LoadRulesAsync(RuleRefreshReason.AfterMutation);
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
            _reordering = false;
        }
    }

    private void DiscardOrderingPreview()
    {
        if (!_reordering)
        {
            _orderingPreview = null;
        }
    }

    private void HandleMutationFailure(ClientError error)
    {
        _state = _state.AfterMutationFailure(error);
        Snackbar.Add(error.Message, Severity.Error);
    }

    private string DescribeStaleState() => _state.StaleReason switch
    {
        RuleSnapshotStaleReason.MutationCommitted => RulesText["StaleMutationCommittedList"],
        RuleSnapshotStaleReason.MutationOutcomeUnknown => RulesText["StaleMutationUnknownList"],
        RuleSnapshotStaleReason.MutationRejectedRequiresRefresh => RulesText["StaleMutationRejectedList"],
        _ => RulesText["StaleRefreshFailedList"],
    };
}
