using System.ComponentModel.DataAnnotations;
using Ufw.Shared.Firewall;

namespace Ufw.Web.Model.V1.KnownHosts;

public abstract class KnownHostRequest
{
    private const int MAX_NAME_LENGTH = 128;
    private const int MAX_ADDRESS_LENGTH = 64;
    private const int MAX_COMMENT_LENGTH = 200;

    [Required]
    [StringLength(MAX_NAME_LENGTH)]
    public string Name { get; init; } = string.Empty;

    [StringLength(MAX_ADDRESS_LENGTH)]
    public string? Address { get; init; }

    public KnownHostAddressSource AddressSource { get; init; } = KnownHostAddressSource.Literal;

    public FirewallAddressFamily? DnsAddressFamily { get; init; }

    [StringLength(MAX_COMMENT_LENGTH)]
    public string? Comment { get; init; }

    public bool IsVisible { get; init; } = true;
}
