using Ufw.Shared.Firewall;

namespace Ufw.Mock.State;

/// <summary>Installed filter tables and the restore inputs captured at the last UFW start/reload.</summary>
internal sealed class UfwMockInstalledState
{
    public bool IPv6EnabledAtInstall { get; set; } = true;
    public MockFilterSnapshot IPv4 { get; set; } = new();
    public MockFilterSnapshot IPv6 { get; set; } = new();
    public List<MockRestoreSource> IPv4Before { get; set; } = [];
    public List<MockRestoreSource> IPv4After { get; set; } = [];
    public List<MockRestoreSource> IPv6Before { get; set; } = [];
    public List<MockRestoreSource> IPv6After { get; set; } = [];

    public MockFilterSnapshot Table(FirewallAddressFamily family) => family switch
    {
        FirewallAddressFamily.IPv4 => IPv4,
        FirewallAddressFamily.IPv6 => IPv6,
        _ => throw new ArgumentOutOfRangeException(nameof(family)),
    };

    public IReadOnlyList<MockRestoreSource> Fixtures(FirewallAddressFamily family, bool before) => (family, before) switch
    {
        (FirewallAddressFamily.IPv4, true) => IPv4Before,
        (FirewallAddressFamily.IPv4, false) => IPv4After,
        (FirewallAddressFamily.IPv6, true) => IPv6Before,
        (FirewallAddressFamily.IPv6, false) => IPv6After,
        _ => throw new ArgumentOutOfRangeException(nameof(family)),
    };
}

internal sealed class MockFilterSnapshot
{
    public List<MockFilterChain> Chains { get; set; } = [];
}

internal sealed class MockFilterChain
{
    public string Name { get; set; } = string.Empty;
    public string Policy { get; set; } = "-";
    public List<string> Rules { get; set; } = [];
}

internal sealed class MockRestoreSource
{
    public string Name { get; set; } = string.Empty;
    public string Contents { get; set; } = string.Empty;
}
