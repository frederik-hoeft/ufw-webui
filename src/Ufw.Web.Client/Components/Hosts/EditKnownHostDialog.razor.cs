using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.KnownHosts.Api;

namespace Ufw.Web.Client.Components.Hosts;

public sealed partial class EditKnownHostDialog
{
    private const int MAX_NAME_LENGTH = 128;
    private const int MAX_ADDRESS_LENGTH = 64;
    private const int MAX_COMMENT_LENGTH = 200;

    private MudForm? _form;
    private Guid? _initializedHostId;
    private string _name = string.Empty;
    private string _address = string.Empty;
    private string? _comment;
    private string? _addressError;
    private bool _isVisible = true;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public KnownHostInventoryItem? Host { get; set; }

    protected override void OnParametersSet()
    {
        if (_initializedHostId == Host?.Id && (Host is not null || _initializedHostId is null))
        {
            return;
        }

        _initializedHostId = Host?.Id;
        _name = Host?.Name ?? string.Empty;
        _address = Host?.Address ?? string.Empty;
        _comment = Host?.Comment;
        _isVisible = Host?.IsVisible ?? true;
        _addressError = null;
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task SaveAsync()
    {
        if (_form is null)
        {
            return;
        }

        _addressError = null;
        await _form.ValidateAsync();
        if (!_form.IsValid)
        {
            return;
        }

        if (!FirewallAddressValue.TryNormalizeLiteral(_address, out string? normalizedAddress, out FirewallAddressFamily addressFamily))
        {
            _addressError = HostsText["InvalidAddress"];
            return;
        }

        if (Host is not null && Host.AddressFamily != addressFamily)
        {
            _addressError = HostsText["AddressFamilyChangeNotAllowed"];
            return;
        }

        string name = _name.Trim();
        if (name.Length == 0)
        {
            return;
        }

        string? comment = string.IsNullOrWhiteSpace(_comment) ? null : _comment.Trim();
        MudDialog.Close(DialogResult.Ok(new KnownHostEditorResult(name, normalizedAddress, comment, _isVisible)));
    }
}
