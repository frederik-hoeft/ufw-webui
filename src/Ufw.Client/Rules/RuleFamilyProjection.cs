using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules;

internal sealed record RuleFamilyProjection(FirewallAddressFamily AddressFamily, IReadOnlyList<RuleRowProjection> Rows);
