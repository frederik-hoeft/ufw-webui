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
    private string? _initializedInitialAddress;
    private bool _initialized;
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

    [Parameter]
    public string? InitialAddress { get; set; }

    private string DnsPreviewAddress => Host?.AddressSource == KnownHostAddressSource.Dns ? _address : string.Empty;

    protected override void OnParametersSet()
    {
        if (_initialized && _initializedHostId == Host?.Id && string.Equals(_initializedInitialAddress, InitialAddress, StringComparison.Ordinal))
        {
            return;
        }

        _initialized = true;
        _initializedHostId = Host?.Id;
        _initializedInitialAddress = InitialAddress;
        _name = Host?.Name ?? string.Empty;
        _address = Host?.Address ?? InitialAddress ?? string.Empty;
        _comment = Host?.Comment;
        _isVisible = Host?.IsVisible ?? true;
        _resolveDns = Host?.AddressSource == KnownHostAddressSource.Dns;
        _dnsAddressFamily = Host?.AddressFamily ?? ResolveInitialAddressFamily(InitialAddress);
        _addressError = null;
    }

    private static FirewallAddressFamily ResolveInitialAddressFamily(string? address) =>
        FirewallAddressValue.TryNormalizeLiteral(address, out _, out FirewallAddressFamily addressFamily) ? addressFamily : FirewallAddressFamily.IPv4;

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
