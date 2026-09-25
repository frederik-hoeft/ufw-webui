using Microsoft.Extensions.Localization;
using MudBlazor;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Services.Localization;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.UI.Components.Hosts;

internal sealed class KnownHostEditorDialogService(
    IDialogService dialogService,
    IKnownHostInventoryService inventory,
    IStringLocalizer<KnownHostsStrings> text) : IKnownHostEditorDialogService
{
    private static readonly DialogOptions s_dialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
    };

    public async Task<KnownHostInventoryResponse?> CreateAsync(string? initialAddress = null, CancellationToken cancellationToken = default)
    {
        DialogParameters<EditKnownHostDialog> parameters = [];
        if (!string.IsNullOrWhiteSpace(initialAddress))
        {
            parameters.Add(component => component.InitialAddress, initialAddress);
        }

        IDialogReference dialog = await dialogService.ShowAsync<EditKnownHostDialog>(text["CreateDialogTitle"], parameters, s_dialogOptions);
        KnownHostEditorResult? result = await dialog.GetReturnValueAsync<KnownHostEditorResult>();
        if (result is null)
        {
            return null;
        }

        return await inventory.CreateAsync(new CreateKnownHostRequest
        {
            Name = result.Name,
            Address = result.Address,
            AddressSource = result.AddressSource,
            DnsAddressFamily = result.DnsAddressFamily,
            Comment = result.Comment,
            IsVisible = result.IsVisible,
        }, cancellationToken);
    }

    public async Task<KnownHostInventoryResponse?> EditAsync(KnownHostInventoryItem host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        DialogParameters<EditKnownHostDialog> parameters = [];
        parameters.Add(component => component.Host, host);
        IDialogReference dialog = await dialogService.ShowAsync<EditKnownHostDialog>(text["EditDialogTitle"], parameters, s_dialogOptions);
        KnownHostEditorResult? result = await dialog.GetReturnValueAsync<KnownHostEditorResult>();
        if (result is null)
        {
            return null;
        }

        return await inventory.UpdateAsync(host.Id, new UpdateKnownHostRequest
        {
            Name = result.Name,
            Address = result.Address,
            AddressSource = result.AddressSource,
            DnsAddressFamily = result.DnsAddressFamily,
            Comment = result.Comment,
            IsVisible = result.IsVisible,
        }, cancellationToken);
    }
}
