using MudBlazor;
using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Client.Api.NetworkInterfaces;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.UI.Components.Interfaces;
using Ufw.Web.Client.UI.Pages.Inventory;
using Ufw.Web.Client.UI.Components;
using Ufw.Web.Model.V1.NetworkInterfaces;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class NetworkInterfacesPage
{
    private static readonly DialogOptions s_commentDialogOptions = ClientDialogOptions.Standard;

    private readonly CancellationTokenSource _lifetime = new();
    private InventoryPageOperations<NetworkInterfaceInventoryResponse> _operations = null!;
    private NetworkInterfaceInventoryResponse? _inventory => _operations.Current;
    private ClientError? _error => _operations.Error;
    private bool _loading => _operations.IsLoading;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(InterfacesText["HostBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(InterfacesText["InterfacesBreadcrumb"], null, disabled: true),
    ];

    private bool IsBusy => _operations.IsBusy;

    protected override Task OnInitializedAsync()
    {
        _operations = new InventoryPageOperations<NetworkInterfaceInventoryResponse>(ClientErrors);
        return RefreshAsync();
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private Task RefreshAsync() => _operations.RefreshAsync(InterfaceInventory.RefreshAsync, _lifetime.Token);

    private Task ReconcileAsync() => _operations.RefreshAsync(
        InterfaceInventory.ReconcileAsync, _lifetime.Token, error => Snackbar.Add(error.Message, Severity.Error));

    private Task EditCommentAsync(NetworkInterfaceInventoryItem item) => SaveAsync(async cancellationToken =>
    {
        DialogParameters<EditNetworkInterfaceCommentDialog> parameters = new()
        {
            { component => component.InterfaceName, item.Name },
            { component => component.Comment, item.Comment }
        };
        IDialogReference dialog = await DialogService.ShowAsync<EditNetworkInterfaceCommentDialog>(InterfacesText["EditCommentTitle"], parameters, s_commentDialogOptions);
        string? comment = await dialog.GetReturnValueAsync<string>();
        if (comment is null)
        {
            return null;
        }

        return await InterfaceInventory.UpdateCommentAsync(item.Id, comment, cancellationToken);
    });

    private Task UpdateVisibilityAsync(NetworkInterfaceInventoryItem item, bool isVisible)
    {
        if (IsBusy || item.IsVisible == isVisible)
        {
            return Task.CompletedTask;
        }

        return SaveAsync(async cancellationToken => await InterfaceInventory.UpdateVisibilityAsync(item.Id, isVisible, cancellationToken));
    }

    private Task<bool> SaveAsync(Func<CancellationToken, Task<NetworkInterfaceInventoryResponse?>> operation)
        => _operations.UpdateAsync(operation, _lifetime.Token, error => Snackbar.Add(error.Message, Severity.Error));

    private string VisibilityActionText(NetworkInterfaceInventoryItem item) => item.IsVisible
        ? InterfacesText["HideFromRuleEditor", item.Name]
        : InterfacesText["ShowInRuleEditor", item.Name];

    private string DescribeCount(int count) => count == 1
        ? InterfacesText["KnownInterfaceCountOne"]
        : InterfacesText["KnownInterfaceCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];

    private string DescribeVisibleCount(int count) => count == 1
        ? InterfacesText["VisibleInterfaceCountOne"]
        : InterfacesText["VisibleInterfaceCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];

    private static string FormatLocalDateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
}
