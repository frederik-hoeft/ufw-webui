using System.ComponentModel.DataAnnotations;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Model.Validation;

namespace Ufw.Web.Model.V1.KnownHosts;

[KnownHostAddressConfiguration]
public abstract class KnownHostRequest
{
    [Required]
    [StringLength(KnownHostLimits.MAX_NAME_LENGTH, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(KnownHostLimits.MAX_ADDRESS_LENGTH)]
    public string? Address { get; init; }

    public KnownHostAddressSource AddressSource { get; init; } = KnownHostAddressSource.Literal;

    public FirewallAddressFamily? DnsAddressFamily { get; init; }

    [StringLength(KnownHostLimits.MAX_COMMENT_LENGTH)]
    public string? Comment { get; init; }

    public bool IsVisible { get; init; } = true;
}
