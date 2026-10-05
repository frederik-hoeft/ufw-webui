using Ufw.Shared.Management.KnownHosts;

namespace Ufw.Web.Data.Model;

internal sealed partial class KnownHostEntry
{
    public long Id { get; set; }

    public Guid PublicId { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }

    public required string Address { get; set; }

    public KnownHostAddressSource AddressSource { get; set; }

    public DateTimeOffset? DnsResolvedAt { get; set; }

    public string? Comment { get; set; }

    public bool IsVisible { get; set; } = true;
}
