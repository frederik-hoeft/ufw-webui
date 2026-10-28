using Ufw.Shared.Firewall;

namespace Ufw.Mock.Formatting;

/// <summary>UFW uses a distinct chain namespace for each address family.</summary>
internal static class MockChainNames
{
    public static string Prefix(FirewallAddressFamily family) => family switch
    {
        FirewallAddressFamily.IPv4 => "ufw-",
        FirewallAddressFamily.IPv6 => "ufw6-",
        _ => throw new ArgumentOutOfRangeException(nameof(family)),
    };

    public static string User(FirewallAddressFamily family, string direction) => Prefix(family) + "user-" + direction;
}
