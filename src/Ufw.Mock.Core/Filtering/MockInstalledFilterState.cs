using Ufw.Mock.Services;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Filtering;

/// <summary>Captures restore inputs at UFW start/reload and materializes the installed tables.</summary>
internal static class MockInstalledFilterState
{
    public static UfwMockInstalledState Install(UfwMockState configuration, UfwMockInstalledState? previous, bool reloadFixtures)
    {
        UfwMockInstalledState installed = previous ?? new();
        if (reloadFixtures || previous is null)
        {
            installed.IPv6EnabledAtInstall = new UfwCompatibilityConfiguration().IsIPv6Enabled(configuration);
            installed.IPv4Before = Capture(FirewallAddressFamily.IPv4, before: true);
            installed.IPv4After = Capture(FirewallAddressFamily.IPv4, before: false);
            installed.IPv6Before = installed.IPv6EnabledAtInstall ? Capture(FirewallAddressFamily.IPv6, before: true) : [];
            installed.IPv6After = installed.IPv6EnabledAtInstall ? Capture(FirewallAddressFamily.IPv6, before: false) : [];
        }
        MockFilterTable ipv4 = new(configuration, FirewallAddressFamily.IPv4);
        ipv4.Add(configuration, FirewallAddressFamily.IPv4, installed);
        MockFilterTable ipv6 = new(configuration, FirewallAddressFamily.IPv6, installed.IPv6EnabledAtInstall);
        ipv6.Add(configuration, FirewallAddressFamily.IPv6, installed);
        installed.IPv4 = ipv4.Snapshot();
        installed.IPv6 = ipv6.Snapshot();
        return installed;
    }

    public static UfwMockInstalledState Stop(UfwMockInstalledState? previous)
    {
        UfwMockInstalledState installed = previous ?? new();
        installed.IPv4 = StopFamily(installed.IPv4, FirewallAddressFamily.IPv4);
        installed.IPv6 = StopFamily(installed.IPv6, FirewallAddressFamily.IPv6);
        return installed;
    }

    public static MockFilterTable Observe(UfwMockState state, FirewallAddressFamily family)
    {
        if (state.Installed is { } installed)
        {
            return new MockFilterTable(installed.Table(family));
        }

        // Legacy schema v1 has no physical snapshot. Preserve its observable
        // state until the next mutation upgrades and persists the installation.
        MockFilterTable legacy = new(state, family);
        legacy.Add(state, family);
        return legacy;
    }

    private static MockFilterSnapshot StopFamily(MockFilterSnapshot previous, FirewallAddressFamily family)
    {
        MockFilterTable table = previous.Chains.Count == 0
            ? new MockFilterTable(UfwMockState.CreateDefault(), family)
            : new MockFilterTable(previous);
        table.StopUfw(family);
        return table.Snapshot();
    }

    private static List<MockRestoreSource> Capture(FirewallAddressFamily family, bool before)
    {
        string variable = family == FirewallAddressFamily.IPv4 ? "UFW_MOCK_IPTABLES4_" : "UFW_MOCK_IPTABLES6_";
        variable += before ? "BEFORE_FILES" : "AFTER_FILES";
        string? paths = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(paths))
        {
            return [];
        }
        return [.. paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => new MockRestoreSource { Name = path, Contents = File.ReadAllText(path) })];
    }
}
