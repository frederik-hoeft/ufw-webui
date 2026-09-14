using MudBlazor;
using Ufw.Client.Api;
using Ufw.Client.Components.Hosts;
using Ufw.Client.Errors;

namespace Ufw.Client.Pages;

public sealed partial class KnownHostsPage
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
    private KnownHostInventoryResponse? _inventory;
    private ClientError? _error;
    private bool _loading;
    private bool _saving;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(HostsText["HostBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(HostsText["HostsBreadcrumb"], null, disabled: true),
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
        _error = null;
        try
        {
            _inventory = await HostInventory.RefreshAsync(_lifetime.Token);
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

    private async Task CreateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IDialogReference dialog = await DialogService.ShowAsync<EditKnownHostDialog>(HostsText["CreateDialogTitle"], s_editorDialogOptions);
        KnownHostEditorResult? result = await dialog.GetReturnValueAsync<KnownHostEditorResult>();
        if (result is null)
        {
            return;
        }

        CreateKnownHostRequest request = new()
        {
            Name = result.Name,
            Address = result.Address,
            Comment = result.Comment,
            IsVisible = result.IsVisible,
        };
        await SaveAsync(cancellationToken => HostInventory.CreateAsync(request, cancellationToken));
    }

    private async Task EditAsync(KnownHostInventoryItem host)
    {
        if (IsBusy)
        {
            return;
        }

        DialogParameters<EditKnownHostDialog> parameters = new();
        parameters.Add(component => component.Host, host);
        IDialogReference dialog = await DialogService.ShowAsync<EditKnownHostDialog>(HostsText["EditDialogTitle"], parameters, s_editorDialogOptions);
        KnownHostEditorResult? result = await dialog.GetReturnValueAsync<KnownHostEditorResult>();
        if (result is null)
        {
            return;
        }

        UpdateKnownHostRequest request = new()
        {
            Name = result.Name,
            Address = result.Address,
            Comment = result.Comment,
            IsVisible = result.IsVisible,
        };
        await SaveAsync(cancellationToken => HostInventory.UpdateAsync(host.Id, request, cancellationToken));
    }

    private async Task DeleteAsync(KnownHostInventoryItem host)
    {
        if (IsBusy)
        {
            return;
        }

        DialogParameters<DeleteKnownHostDialog> parameters = new();
        parameters.Add(component => component.Host, host);
        IDialogReference dialog = await DialogService.ShowAsync<DeleteKnownHostDialog>(HostsText["DeleteDialogTitle"], parameters, s_deleteDialogOptions);
        bool? confirmed = await dialog.GetReturnValueAsync<bool>();
        if (confirmed != true)
        {
            return;
        }

        await SaveAsync(cancellationToken => HostInventory.DeleteAsync(host.Id, cancellationToken));
    }

    private async Task UpdateVisibilityAsync(KnownHostInventoryItem host, bool isVisible)
    {
        if (IsBusy || host.IsVisible == isVisible)
        {
            return;
        }

        UpdateKnownHostRequest request = new()
        {
            Name = host.Name,
            Address = host.Address,
            Comment = host.Comment,
            IsVisible = isVisible,
        };
        await SaveAsync(cancellationToken => HostInventory.UpdateAsync(host.Id, request, cancellationToken));
    }

    private async Task SaveAsync(Func<CancellationToken, Task<KnownHostInventoryResponse>> operation)
    {
        _saving = true;
        _error = null;
        try
        {
            _inventory = await operation(_lifetime.Token);
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
            _saving = false;
        }
    }

    private string VisibilityActionText(KnownHostInventoryItem host) => host.IsVisible
        ? HostsText["HideFromRuleEditor", host.Name]
        : HostsText["ShowInRuleEditor", host.Name];

    private string DescribeCount(int count) => count == 1
        ? HostsText["KnownHostCountOne"]
        : HostsText["KnownHostCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];

    private string DescribeVisibleCount(int count) => count == 1
        ? HostsText["VisibleHostCountOne"]
        : HostsText["VisibleHostCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];
}
