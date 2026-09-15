using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules;

public sealed record RuleFamilyProjection(FirewallAddressFamily AddressFamily, IReadOnlyList<RuleRowProjection> Rows);
