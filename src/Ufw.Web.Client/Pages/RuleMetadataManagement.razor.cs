using MudBlazor;
using Ufw.Web.Client.Components.Rules.Metadata;

namespace Ufw.Web.Client.Pages;

public sealed partial class RuleMetadataManagement
{
    private static readonly DialogOptions s_tagManagerDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
    };

    private static readonly DialogOptions s_reconciliationDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.Medium,
    };

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
