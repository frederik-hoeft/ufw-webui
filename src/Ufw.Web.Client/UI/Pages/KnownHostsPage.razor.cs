using MudBlazor;
using Ufw.Web.Client.UI.Components.Hosts;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class KnownHostsPage
{
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

    private Task CreateAsync() => RunEditorAsync(cancellationToken => HostEditor.CreateAsync(cancellationToken: cancellationToken));

    private Task EditAsync(KnownHostInventoryItem host) => RunEditorAsync(cancellationToken => HostEditor.EditAsync(host, cancellationToken));

    private async Task RunEditorAsync(Func<CancellationToken, Task<KnownHostInventoryResponse?>> operation)
    {
        if (IsBusy)
        {
            return;
        }

        _saving = true;
        _error = null;
        try
        {
            KnownHostInventoryResponse? response = await operation(_lifetime.Token);
            if (response is not null)
            {
                _inventory = response;
            }
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

    private async Task ReconcileDnsAsync(KnownHostInventoryItem host)
    {
        if (IsBusy || host.AddressSource != KnownHostAddressSource.Dns)
        {
            return;
        }

        if (await SaveAsync(cancellationToken => HostInventory.ReconcileDnsAsync(host.Id, cancellationToken)))
        {
            Snackbar.Add(HostsText["DnsReconciled", host.Name], Severity.Success);
        }
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
            Address = host.AddressSource == KnownHostAddressSource.Literal ? host.Address : null,
            AddressSource = host.AddressSource,
            DnsAddressFamily = host.AddressSource == KnownHostAddressSource.Dns ? host.AddressFamily : null,
            Comment = host.Comment,
            IsVisible = isVisible,
        };
        await SaveAsync(cancellationToken => HostInventory.UpdateAsync(host.Id, request, cancellationToken));
    }

    private async Task<bool> SaveAsync(Func<CancellationToken, Task<KnownHostInventoryResponse>> operation)
    {
        _saving = true;
        _error = null;
        try
        {
            _inventory = await operation(_lifetime.Token);
            return true;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
            Snackbar.Add(_error.Message, Severity.Error);
            return false;
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

    private string DescribeDnsResolvedAt(DateTimeOffset resolvedAt) => HostsText["DnsResolvedAt", FormatLocalDateTime(resolvedAt)];

    private static string FormatLocalDateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
}
