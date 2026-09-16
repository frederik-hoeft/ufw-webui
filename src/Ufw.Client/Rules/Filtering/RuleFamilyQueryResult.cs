using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering;

internal sealed record RuleFamilyQueryResult(FirewallAddressFamily AddressFamily, IReadOnlyList<RuleQueryRow> Rows, int TotalCount);
