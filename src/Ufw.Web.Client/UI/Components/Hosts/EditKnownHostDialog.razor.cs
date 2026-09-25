using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Shared.Firewall;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.UI.Components.Hosts;

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
    private bool _resolveDns;
    private FirewallAddressFamily _dnsAddressFamily = FirewallAddressFamily.IPv4;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public KnownHostInventoryItem? Host { get; set; }

    private string DnsPreviewAddress => Host?.AddressSource == KnownHostAddressSource.Dns ? _address : string.Empty;

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
        _resolveDns = Host?.AddressSource == KnownHostAddressSource.Dns;
        _dnsAddressFamily = Host?.AddressFamily ?? FirewallAddressFamily.IPv4;
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

        string? normalizedAddress = null;
        FirewallAddressFamily? dnsAddressFamily = null;
        KnownHostAddressSource addressSource = _resolveDns ? KnownHostAddressSource.Dns : KnownHostAddressSource.Literal;
        if (_resolveDns)
        {
            dnsAddressFamily = _dnsAddressFamily;
        }
        else
        {
            if (!FirewallAddressValue.TryNormalizeLiteral(_address, out normalizedAddress, out FirewallAddressFamily addressFamily))
            {
                _addressError = HostsText["InvalidAddress"];
                return;
            }
            if (Host is not null && Host.AddressFamily != addressFamily)
            {
                _addressError = HostsText["AddressFamilyChangeNotAllowed"];
                return;
            }
        }

        string name = _name.Trim();
        if (name.Length == 0)
        {
            return;
        }

        string? comment = string.IsNullOrWhiteSpace(_comment) ? null : _comment.Trim();
        MudDialog.Close(DialogResult.Ok(new KnownHostEditorResult(name, normalizedAddress, addressSource, dnsAddressFamily, comment, _isVisible)));
    }
}
