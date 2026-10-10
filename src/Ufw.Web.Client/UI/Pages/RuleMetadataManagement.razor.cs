using MudBlazor;
using Ufw.Web.Client.UI.Components.Rules.Metadata;
using Ufw.Web.Client.UI.Components;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class RuleMetadataManagement
{
    private static readonly DialogOptions s_tagManagerDialogOptions = ClientDialogOptions.Standard;

    private static readonly DialogOptions s_reconciliationDialogOptions = ClientDialogOptions.Wide;

    private bool _dialogOpen;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["MetadataManagementTitle"], null, disabled: true),
    ];

    private async Task ManageTagsAsync()
    {
        if (_dialogOpen)
        {
            return;
        }

        _dialogOpen = true;
        try
        {
            IDialogReference dialog = await DialogService.ShowAsync<ManageRuleTagsDialog>(RulesText["ManageTags"], s_tagManagerDialogOptions);
            await dialog.Result;
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async Task ReconcileMetadataAsync()
    {
        if (_dialogOpen)
        {
            return;
        }

        _dialogOpen = true;
        try
        {
            IDialogReference dialog = await DialogService.ShowAsync<ReconcileRuleMetadataDialog>(RulesText["ReconcileMetadata"], s_reconciliationDialogOptions);
            await dialog.Result;
        }
        finally
        {
            _dialogOpen = false;
        }
    }
}
