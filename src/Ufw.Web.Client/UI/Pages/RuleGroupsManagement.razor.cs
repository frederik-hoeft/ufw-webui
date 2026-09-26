using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Api.Rules;
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
        MaxWidth = MaxWidth.ExtraSmall,
    };

    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<Guid> _expandedGroups = [];
    private IReadOnlyList<RuleGroup> _catalogGroups = [];
    private IReadOnlyList<RuleGroupManagementProjection> _groups = [];
    private RuleSnapshot? _ruleSnapshot;
    private ClientError? _groupError;
    private ClientError? _ruleInventoryError;
    private bool _loading;
    private bool _saving;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["MetadataManagementTitle"], "/metadata"),
        new BreadcrumbItem(RulesText["ManageGroupsTitle"], null, disabled: true),
    ];

    private bool IsBusy => _loading || _saving;

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
        _groupError = null;
        _ruleInventoryError = null;
        try
        {
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
        if (IsBusy || group.RuleIds.Count != 0)
        {
            return;
        }

        DialogParameters<DeleteRuleGroupDialog> parameters = [];
        parameters.Add(component => component.Group, group);
        IDialogReference dialog = await DialogService.ShowAsync<DeleteRuleGroupDialog>(RulesText["DeleteGroup"], parameters, s_deleteDialogOptions);
        if (await dialog.GetReturnValueAsync<bool?>() != true)
        {
            return;
        }

        _saving = true;
        try
        {
            _catalogGroups = await GroupCatalog.DeleteAsync(group.Id, _lifetime.Token);
            _expandedGroups.Remove(group.Id);
            RebuildProjection();
            Snackbar.Add(RulesText["GroupDeleted"], Severity.Success);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            ClientError error = ClientErrors.Describe(exception);
            Snackbar.Add(error.Message, Severity.Error);
        }
        finally
        {
            _saving = false;
        }
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
}
