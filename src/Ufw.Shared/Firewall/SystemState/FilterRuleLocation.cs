namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Physical identity of one rule occurrence in a filter table.
/// <see cref="Index"/> is the zero-based position in <see cref="ChainName"/>.
/// </summary>
public readonly record struct FilterRuleLocation(FirewallAddressFamily AddressFamily, string ChainName, int Index);
