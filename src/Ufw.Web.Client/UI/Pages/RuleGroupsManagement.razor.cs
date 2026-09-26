using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.UI.Components.Rules.Metadata;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class RuleGroupsManagement
{
    private static readonly DialogOptions s_editorDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
    };

    private static readonly DialogOptions s_deleteDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
    };

    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<Guid> _expandedGroups = [];
    private IReadOnlyList<RuleGroup> _catalogGroups = [];
    private IReadOnlyList<RuleGroupManagementProjection> _groups = [];
    private RuleSnapshot? _ruleSnapshot;
    private ClientError? _groupError;
    private ClientError? _ruleInventoryError;
    private RuleGroupDeletionResultContext? _deletionResult;
    private bool _loading;
    private bool _saving;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["MetadataManagementTitle"], "/metadata"),
        new BreadcrumbItem(RulesText["ManageGroupsTitle"], null, disabled: true),
    ];

    private bool IsBusy => _loading || _saving;

    private Severity DeletionResultSeverity => _deletionResult?.Result switch
    {
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupChanged } => Severity.Warning,
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupRetained } => Severity.Warning,
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupCleanupFailed } => Severity.Error,
        { BatchResponse.Outcome: RuleBatchDeleteOutcome.StateUncertain } => Severity.Error,
        { Outcome: RuleGroupDeletionWorkflowOutcome.BatchIncomplete } => Severity.Warning,
        _ => Severity.Info,
    };

    private string DeletionResultTitle => _deletionResult?.Result switch
    {
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupChanged } => RulesText["GroupDeleteMembershipChangedTitle"],
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupRetained, BatchResponse: null } => RulesText["GroupDeleteConcurrentMembershipTitle"],
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupRetained } => RulesText["GroupDeleteRulesCompletedGroupRetainedTitle"],
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupCleanupFailed } => RulesText["GroupDeleteCleanupFailedTitle"],
        { BatchResponse.Outcome: RuleBatchDeleteOutcome.StateUncertain } => RulesText["GroupDeleteStateUncertainTitle"],
        { BatchResponse.Outcome: RuleBatchDeleteOutcome.PartiallyCompleted } => RulesText["GroupDeletePartialTitle"],
        { BatchResponse.Outcome: RuleBatchDeleteOutcome.StaleBaseline } => RulesText["GroupDeleteStaleTitle"],
        { BatchResponse.Outcome: RuleBatchDeleteOutcome.PreconditionFailed } => RulesText["GroupDeletePreconditionTitle"],
        _ => RulesText["DeleteGroup"],
    };

    private string DeletionResultDescription => _deletionResult?.Result switch
    {
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupChanged } => RulesText["GroupDeleteMembershipChangedDescription", _deletionResult.GroupName],
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupRetained, BatchResponse: null } => RulesText["GroupDeleteConcurrentMembershipDescription", _deletionResult.GroupName],
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupRetained } => RulesText["GroupDeleteRulesCompletedGroupRetainedDescription", _deletionResult.GroupName],
        { Outcome: RuleGroupDeletionWorkflowOutcome.GroupCleanupFailed } => RulesText["GroupDeleteCleanupFailedDescription", _deletionResult.GroupName],
        { BatchResponse.Outcome: RuleBatchDeleteOutcome.StateUncertain } => RulesText["GroupDeleteStateUncertainDescription", _deletionResult.GroupName],
        { BatchResponse.Outcome: RuleBatchDeleteOutcome.PartiallyCompleted } => RulesText["GroupDeletePartialDescription", _deletionResult.GroupName],
        { BatchResponse.Outcome: RuleBatchDeleteOutcome.StaleBaseline } => RulesText["GroupDeleteStaleDescription", _deletionResult.GroupName],
        { BatchResponse.Outcome: RuleBatchDeleteOutcome.PreconditionFailed } => RulesText["GroupDeletePreconditionDescription", _deletionResult.GroupName],
        _ => string.Empty,
    };

    protected override Task OnInitializedAsync() => RefreshAsync();

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _loading = true;
        try
        {
            await RefreshStateAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            _loading = false;
        }
    }

    private Task CreateAsync() => ShowEditorAsync(group: null);

    private Task EditAsync(RuleGroup group) => ShowEditorAsync(group);

    private async Task ShowEditorAsync(RuleGroup? group)
    {
        if (IsBusy)
        {
            return;
        }

        _saving = true;
        try
        {
            DialogParameters<EditRuleGroupDialog> parameters = [];
            parameters.Add(component => component.Group, group);
            string title = group is null ? RulesText["CreateGroup"] : RulesText["EditGroup"];
            IDialogReference dialog = await DialogService.ShowAsync<EditRuleGroupDialog>(title, parameters, s_editorDialogOptions);
            if (await dialog.GetReturnValueAsync<bool?>() != true)
            {
                return;
            }

            _catalogGroups = GroupCatalog.Current;
            RebuildProjection();
            Snackbar.Add(group is null ? RulesText["GroupCreated"] : RulesText["GroupSaved"], Severity.Success);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task DeleteAsync(RuleGroup group)
    {
        if (IsBusy)
        {
            return;
        }

        await RefreshAsync();
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }
        if (_groupError is not null)
        {
            Snackbar.Add(_groupError.Message, Severity.Error);
            return;
        }

        RuleGroupManagementProjection? projection = _groups.SingleOrDefault(candidate => candidate.Group.Id == group.Id);
        if (projection is null)
        {
            Snackbar.Add(RulesText["GroupNoLongerAvailable"], Severity.Warning);
            return;
        }

        DialogParameters<DeleteRuleGroupDialog> parameters = [];
        parameters.Add(component => component.Projection, projection);
        IDialogReference dialog = await DialogService.ShowAsync<DeleteRuleGroupDialog>(RulesText["DeleteGroup"], parameters, s_deleteDialogOptions);
        RuleGroupDeleteDialogResult? confirmation = await dialog.GetReturnValueAsync<RuleGroupDeleteDialogResult>();
        if (confirmation is null)
        {
            return;
        }

        _saving = true;
        _deletionResult = null;
        string? privateKey = confirmation.PrivateKey;
        try
        {
            RuleGroupDeletionWorkflowResult result = await GroupDeletion.DeleteAsync(projection, _ruleSnapshot, privateKey, _lifetime.Token);
            _catalogGroups = result.Groups;
            await RefreshStateAsync();
            _deletionResult = result.Outcome == RuleGroupDeletionWorkflowOutcome.Deleted
                ? null
                : new RuleGroupDeletionResultContext(projection.Group.Name, result);

            if (result.Outcome == RuleGroupDeletionWorkflowOutcome.Deleted)
            {
                _expandedGroups.Remove(projection.Group.Id);
                Snackbar.Add(RulesText["GroupDeleted"], Severity.Success);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            ClientError error = ClientErrors.Describe(exception);
            Snackbar.Add(error.Message, Severity.Error);
        }
        catch (InvalidOperationException exception)
        {
            Snackbar.Add(exception.Message, Severity.Warning);
        }
        finally
        {
            privateKey = string.Empty;
            _saving = false;
        }
    }

    private async Task RefreshStateAsync()
    {
        _groupError = null;
        _ruleInventoryError = null;
        Task<IReadOnlyList<RuleGroup>> groupsTask = GroupCatalog.RefreshAsync(_lifetime.Token);
        Task<RuleInventoryResponse> rulesTask = RuleApiClient.GetInventoryAsync(_lifetime.Token);

        try
        {
            _catalogGroups = await groupsTask;
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _groupError = ClientErrors.Describe(exception);
            _catalogGroups = GroupCatalog.Current;
        }

        try
        {
            RuleInventoryResponse response = await rulesTask;
            _ruleSnapshot = RuleSnapshot.FromResponse(response);
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _ruleInventoryError = ClientErrors.Describe(exception);
            _ruleSnapshot = null;
        }

        RebuildProjection();
    }

    private void RebuildProjection()
    {
        if (_ruleSnapshot is not null)
        {
            _ruleSnapshot = _ruleSnapshot.ReconcileGroupCatalog(_catalogGroups);
        }
        _groups = GroupProjection.Create(_catalogGroups, _ruleSnapshot);
        HashSet<Guid> currentIds = [.. _groups.Select(static group => group.Group.Id)];
        _expandedGroups.RemoveWhere(groupId => !currentIds.Contains(groupId));
    }

    private bool IsExpanded(Guid groupId) => _expandedGroups.Contains(groupId);

    private void ToggleExpanded(Guid groupId)
    {
        if (!_expandedGroups.Add(groupId))
        {
            _expandedGroups.Remove(groupId);
        }
    }

    private void HandleKeyDown(KeyboardEventArgs args, Guid groupId)
    {
        if (args.Key is "Enter" or " ")
        {
            ToggleExpanded(groupId);
        }
    }

    private string DescribeBatchDeleteProgress(RuleBatchDeleteResponse response)
    {
        int deleted = response.Operations.Count(static operation => operation.Outcome is RuleBatchDeleteOperationOutcome.Deleted or RuleBatchDeleteOperationOutcome.DeletedAfterProcessFailure);
        int failed = response.Operations.Count(static operation => operation.Outcome == RuleBatchDeleteOperationOutcome.Failed);
        int uncertain = response.Operations.Count(static operation => operation.Outcome == RuleBatchDeleteOperationOutcome.StateUncertain);
        return RulesText["GroupDeleteBatchProgress", deleted, failed, uncertain, response.PendingOccurrenceIds.Count];
    }

    private string DescribeGroupCount(int count) => count == 1
        ? RulesText["GroupCountOne"]
        : RulesText["GroupCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];

    private string DescribeGroupedRuleCount(int count) => count == 1
        ? RulesText["GroupedRuleCountOne"]
        : RulesText["GroupedRuleCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];

    private string DescribeMemberCount(RuleGroupManagementProjection group)
    {
        string count = group.StoredMemberCount == 1
            ? RulesText["GroupMemberCountOne"]
            : RulesText["GroupMemberCountMany", group.StoredMemberCount.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];
        if (group.MemberResolutionAvailable && group.StaleMembershipCount > 0)
        {
            return RulesText["GroupMemberCountWithStale", count, group.StaleMembershipCount.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];
        }
        return count;
    }

    private sealed record RuleGroupDeletionResultContext(string GroupName, RuleGroupDeletionWorkflowResult Result);
}
