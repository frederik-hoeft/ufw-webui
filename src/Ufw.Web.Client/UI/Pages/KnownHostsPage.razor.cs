using MudBlazor;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.UI.Components.Hosts;
using Ufw.Web.Client.UI.Pages.Inventory;
using Ufw.Web.Model.V1.KnownHosts;

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
    private InventoryPageOperations<KnownHostInventoryResponse> _operations = null!;
    private KnownHostInventoryResponse? _inventory => _operations.Current;
    private ClientError? _error => _operations.Error;
    private bool _loading => _operations.IsLoading;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(HostsText["HostBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(HostsText["HostsBreadcrumb"], null, disabled: true),
    ];

    private bool IsBusy => _operations.IsBusy;

    protected override Task OnInitializedAsync()
    {
        _operations = new InventoryPageOperations<KnownHostInventoryResponse>(ClientErrors);
        return RefreshAsync();
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private Task RefreshAsync() => _operations.RefreshAsync(HostInventory.RefreshAsync, _lifetime.Token);

    private Task CreateAsync() => SaveAsync(cancellationToken => HostEditor.CreateAsync(cancellationToken: cancellationToken));

    private Task EditAsync(KnownHostInventoryItem host) => SaveAsync(cancellationToken => HostEditor.EditAsync(host, cancellationToken));

    private async Task ReconcileDnsAsync(KnownHostInventoryItem host)
    {
        if (IsBusy || host.AddressSource != KnownHostAddressSource.Dns)
        {
            return;
        }

        bool saved = await SaveAsync(async cancellationToken => await HostInventory.ReconcileDnsAsync(host.Id, cancellationToken));
        if (saved)
        {
            Snackbar.Add(HostsText["DnsReconciled", host.Name], Severity.Success);
        }
    }

    private Task DeleteAsync(KnownHostInventoryItem host) => SaveAsync(async cancellationToken =>
    {
        DialogParameters<DeleteKnownHostDialog> parameters = new()
        {
            { component => component.Host, host }
        };
        IDialogReference dialog = await DialogService.ShowAsync<DeleteKnownHostDialog>(HostsText["DeleteDialogTitle"], parameters, s_deleteDialogOptions);
        bool? confirmed = await dialog.GetReturnValueAsync<bool>();
        if (confirmed != true)
        {
            return null;
        }

        return await HostInventory.DeleteAsync(host.Id, cancellationToken);
    });

    private Task UpdateVisibilityAsync(KnownHostInventoryItem host, bool isVisible)
    {
        if (IsBusy || host.IsVisible == isVisible)
        {
            return Task.CompletedTask;
        }

        return SaveAsync(async cancellationToken => await HostInventory.UpdateVisibilityAsync(host, isVisible, cancellationToken));
    }

    private Task<bool> SaveAsync(Func<CancellationToken, Task<KnownHostInventoryResponse?>> operation)
        => _operations.UpdateAsync(operation, _lifetime.Token, error => Snackbar.Add(error.Message, Severity.Error));

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
