using MudBlazor;
using Ufw.Client.Api;
using Ufw.Client.Components.Interfaces;
using Ufw.Client.Errors;

namespace Ufw.Client.Pages;

public sealed partial class NetworkInterfaces
{
    private static readonly DialogOptions s_commentDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
    };

    private readonly CancellationTokenSource _lifetime = new();
    private NetworkInterfaceInventoryResponse? _inventory;
    private ClientError? _error;
    private bool _loading;
    private bool _savingMetadata;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(InterfacesText["HostBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(InterfacesText["InterfacesBreadcrumb"], null, disabled: true),
    ];

    private bool IsBusy => _loading || _savingMetadata;

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
        _error = null;
        try
        {
            _inventory = await InterfaceInventory.RefreshAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ReconcileAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _loading = true;
        _error = null;
        try
        {
            _inventory = await InterfaceInventory.ReconcileAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
            Snackbar.Add(_error.Message, Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task EditCommentAsync(NetworkInterfaceInventoryItem item)
    {
        if (IsBusy)
        {
            return;
        }

        DialogParameters<EditNetworkInterfaceCommentDialog> parameters = new();
        parameters.Add(component => component.InterfaceName, item.Name);
        parameters.Add(component => component.Comment, item.Comment);
        IDialogReference dialog = await DialogService.ShowAsync<EditNetworkInterfaceCommentDialog>(InterfacesText["EditCommentTitle"], parameters, s_commentDialogOptions);
        string? comment = await dialog.GetReturnValueAsync<string>();
        if (comment is null)
        {
            return;
        }

        _savingMetadata = true;
        try
        {
            _inventory = await InterfaceInventory.UpdateCommentAsync(item.Id, comment, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
            Snackbar.Add(_error.Message, Severity.Error);
        }
        finally
        {
            _savingMetadata = false;
        }
    }

    private async Task UpdateVisibilityAsync(NetworkInterfaceInventoryItem item, bool isVisible)
    {
        if (IsBusy || item.IsVisible == isVisible)
        {
            return;
        }

        _savingMetadata = true;
        _error = null;
        try
        {
            _inventory = await InterfaceInventory.UpdateVisibilityAsync(item.Id, isVisible, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
            Snackbar.Add(_error.Message, Severity.Error);
        }
        finally
        {
            _savingMetadata = false;
        }
    }

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
