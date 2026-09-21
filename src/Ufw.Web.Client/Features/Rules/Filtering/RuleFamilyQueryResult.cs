using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering;

internal sealed record RuleFamilyQueryResult(FirewallAddressFamily AddressFamily, IReadOnlyList<RuleQueryRow> Rows, int TotalCount);
