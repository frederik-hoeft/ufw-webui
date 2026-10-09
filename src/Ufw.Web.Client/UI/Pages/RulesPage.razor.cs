using MudBlazor;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Ordering;
using Ufw.Web.Client.Features.Rules.Services;
using Ufw.Web.Client.Features.Rules.Templates;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.UI.Components.Rules;
using Ufw.Web.Client.UI.Components.Rules.Metadata;
using Ufw.Web.Client.UI.Components.Rules.Templates;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class RulesPage
{
    private static readonly DialogOptions s_mutationDialogOptions = new()
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

    private static readonly DialogOptions s_templateDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.ExtraSmall,
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

    private bool FirewallModelClean => _state.Snapshot?.Assessment.IsClean == true;

    private bool CanMutateFirewall => _state.IsCurrent && FirewallModelClean && _pageInteraction.CanMutateFirewall && !HasOrderingPreview;

    private bool CanEditMetadata => _state.IsCurrent && _pageInteraction.CanEditMetadata;

    private bool CanSaveTemplate => _state.Snapshot is not null && !_state.IsLoading && _pageInteraction.CanSaveTemplate && !HasOrderingPreview;

    private bool CanPreviewOrdering => _state.IsCurrent && FirewallModelClean && _pageInteraction.CanPreviewOrdering && InteractionState.CanOrder;

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
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
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
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
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
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
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

    private async Task SaveAsTemplateAsync(RuleRowProjection row)
    {
        if (!CanSaveTemplate || !row.CanSaveAsTemplate || row.Rule.Rule is null)
        {
            return;
        }

        _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.TemplateDialogOpened());
        try
        {
            DialogParameters<SaveRuleAsTemplateDialog> parameters = [];
            parameters.Add(component => component.CanonicalCommand, row.CanonicalCommand);
            IDialogReference dialog = await DialogService.ShowAsync<SaveRuleAsTemplateDialog>(TemplatesText["SaveAsTemplate"], parameters, s_templateDialogOptions);
            SaveRuleAsTemplateDialogResult? result = await dialog.GetReturnValueAsync<SaveRuleAsTemplateDialogResult>();
            if (result is null)
            {
                return;
            }

            _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.TemplateSaveStarted());
            RuleTemplateDefinition definition = TemplateAuthoring.CreateDefinition(result.Name, result.Description, row.Rule.Rule, row.Metadata);
            _ = await TemplateCatalog.CreateAsync(definition, _lifetime.Token);
            _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.TemplateSaveCompleted());
            Snackbar.Add(TemplatesText["TemplateSavedFromRule"], Severity.Success);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            Snackbar.Add(ClientErrors.Describe(exception).Message, Severity.Error);
        }
        finally
        {
            if (_pageInteraction.Mode == RulesPageInteractionMode.TemplateSaving)
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.TemplateSaveCompleted());
            }
            if (_pageInteraction.Mode == RulesPageInteractionMode.TemplateDialog)
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.TemplateDialogClosed());
            }
        }
    }

    private async Task BeginDisableAsync(RuleRowProjection row)
    {
        if (!CanMutateFirewall || !row.CanMutate || row.Rule.Rule is null || string.IsNullOrWhiteSpace(row.Rule.RuleId))
        {
            return;
        }

        DisableRuleDialogResult? confirmation = null;
        _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DisableDialogOpened());
        try
        {
            DialogParameters<DisableRuleDialog> parameters = [];
            parameters.Add(component => component.Row, row);
            IDialogReference dialog = await DialogService.ShowAsync<DisableRuleDialog>(TemplatesText["DisableRuleTitle"], parameters, s_mutationDialogOptions);
            confirmation = await dialog.GetReturnValueAsync<DisableRuleDialogResult>();
        }
        finally
        {
            if (confirmation is null && _pageInteraction.Mode == RulesPageInteractionMode.DisableDialog)
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DisableDialogClosed());
            }
        }

        if (confirmation is null)
        {
            return;
        }

        string privateKey = confirmation.PrivateKey;
        try
        {
            if (_lifetime.IsCancellationRequested)
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DisableDialogClosed());
                return;
            }

            _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DisableConfirmed());
            RuleDisableWorkflowResult result = await RuleDisable.DisableAsync(row, confirmation.Name, confirmation.Description, privateKey, _lifetime.Token);
            switch (result.Outcome)
            {
                case RuleDisableWorkflowOutcome.Completed:
                    _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DisableCompleted());
                    Snackbar.Add(TemplatesText["DisableCompleted", result.TemplateName], Severity.Success);
                    await LoadRulesAsync(RuleInventoryRefreshReason.AfterMutation);
                    break;
                case RuleDisableWorkflowOutcome.TemplatePersistenceNotConfirmed:
                {
                    ClientError error = result.Error ?? throw new InvalidOperationException("A template-persistence failure must include a client error.");
                    Snackbar.Add(TemplatesText["DisableTemplatePersistenceNotConfirmed", error.Message], Severity.Error);
                    break;
                }
                case RuleDisableWorkflowOutcome.FirewallDeleteNotConfirmed:
                {
                    ClientError error = result.Error ?? throw new InvalidOperationException("A firewall-delete failure must include a client error.");
                    _state = _state.MoveNext(new RuleInventoryTransition.MutationFailed(error));
                    Snackbar.Add(TemplatesText["DisableFirewallDeleteNotConfirmed", result.TemplateName, error.Message], Severity.Error);
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unsupported rule-disable workflow outcome '{result.Outcome}'.");
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ClientError error = ClientErrors.Describe(exception);
            Snackbar.Add(TemplatesText["DisableUnexpectedFailure", error.Message], Severity.Error);
        }
        finally
        {
            privateKey = string.Empty;
            if (_pageInteraction.Mode == RulesPageInteractionMode.Disabling)
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DisableCompleted());
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
                GroupId = result.GroupId,
            }, _lifetime.Token);
            _state = _state.MoveNext(new RuleInventoryTransition.MetadataMutationCompleted(ruleId, response));
            RefreshRuleListProjection();
            Snackbar.Add(RulesText["MetadataSaved"], Severity.Success);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
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
        if (!CanMutateFirewall || !rule.Parsed || rule.Rule is null || string.IsNullOrWhiteSpace(rule.RuleId) || _state.Snapshot is not { } snapshot)
        {
            return;
        }

        RuleGroup? orphanGroupCandidate = null;
        try
        {
            orphanGroupCandidate = await GroupDeletion.GetSingleRuleCleanupCandidateAsync(rule, snapshot, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            Snackbar.Add(RulesText["DeleteGroupCleanupOptionUnavailable"], Severity.Warning);
        }

        DeleteRuleDialogResult? confirmation = null;
        _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteDialogOpened());
        try
        {
            DialogParameters<DeleteRuleDialog> parameters = new()
            {
                { component => component.Rule, rule },
                { component => component.OrphanGroupCandidate, orphanGroupCandidate },
            };

            IDialogReference dialog = await DialogService.ShowAsync<DeleteRuleDialog>(RulesText["DeleteDialogTitle"], parameters, s_mutationDialogOptions);
            confirmation = await dialog.GetReturnValueAsync<DeleteRuleDialogResult>();
        }
        finally
        {
            if (confirmation is null)
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteDialogClosed());
            }
        }

        if (confirmation is null)
        {
            return;
        }

        string privateKey = confirmation.PrivateKey;
        try
        {
            if (_lifetime.IsCancellationRequested)
            {
                _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteDialogClosed());
                return;
            }

            _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteConfirmed());
            await DeleteRuleAsync(rule, privateKey, confirmation.DeleteGroup ? orphanGroupCandidate : null);
        }
        finally
        {
            privateKey = string.Empty;
        }
    }

    private async Task DeleteRuleAsync(ListedFirewallRule rule, string privateKey, RuleGroup? groupToDelete)
    {
        try
        {
            await RuleMutations.DeleteRuleAsync(rule, privateKey, _lifetime.Token);
            if (groupToDelete is not null)
            {
                await TryDeleteOrphanedGroupAsync(groupToDelete);
            }

            _pageInteraction = _pageInteraction.MoveNext(new RulesPageInteractionTransition.DeleteCompleted());
            await LoadRulesAsync(RuleInventoryRefreshReason.AfterMutation);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
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

    private async Task TryDeleteOrphanedGroupAsync(RuleGroup group)
    {
        try
        {
            RuleGroupCleanupResult cleanup = await GroupDeletion.DeleteIfEmptyAsync(group.Id, _lifetime.Token);
            Snackbar.Add(cleanup.Deleted ? RulesText["RuleAndGroupDeleted", group.Name] : RulesText["RuleDeletedGroupRetained", group.Name], cleanup.Deleted ? Severity.Success : Severity.Warning);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            Snackbar.Add(RulesText["RuleDeletedGroupCleanupFailed", group.Name, ClientErrors.Describe(exception).Message], Severity.Warning);
        }
    }

    private Task BeginRuleEditAsync(RuleRowProjection row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!CanMutateFirewall || !row.CanEdit || _state.Snapshot is not { } snapshot)
        {
            return Task.CompletedTask;
        }

        if (row.Rule.Rule?.AddressFamily == FirewallAddressFamily.IPv6 && !snapshot.Configuration.IPv6Enabled)
        {
            Snackbar.Add(RulesText["ReplacementIPv6Unavailable"], Severity.Warning);
            return Task.CompletedTask;
        }

        try
        {
            RuleListResponse baseline = new(snapshot.FirewallActive, snapshot.Rules, snapshot.Configuration);
            string uri = ReplacementNavigation.BuildUri(baseline, row.OccurrenceId);
            Navigation.NavigateTo(uri);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            Snackbar.Add(RulesText["ReplacementTargetUnavailable"], Severity.Warning);
        }

        return Task.CompletedTask;
    }

    private Task BeginOrderedInsertionAsync(RuleInsertionActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanMutateFirewall || _state.Snapshot is not { } snapshot)
        {
            return Task.CompletedTask;
        }

        RuleSnapshotIndex index = new(snapshot.Rules);
        if (!index.TryGet(request.OccurrenceId, out ListedFirewallRule? anchor))
        {
            Snackbar.Add(RulesText["InsertionTargetUnavailable"], Severity.Warning);
            return Task.CompletedTask;
        }
        if (anchor.Rule?.AddressFamily == FirewallAddressFamily.IPv6 && !snapshot.Configuration.IPv6Enabled)
        {
            Snackbar.Add(RulesText["InsertionIPv6Unavailable"], Severity.Warning);
            return Task.CompletedTask;
        }

        try
        {
            RuleListResponse baseline = new(snapshot.FirewallActive, snapshot.Rules, snapshot.Configuration);
            string uri = InsertionNavigation.BuildUri(baseline, request.OccurrenceId, request.Placement);
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
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
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
