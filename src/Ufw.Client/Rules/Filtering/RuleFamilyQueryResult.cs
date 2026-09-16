using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering;

internal sealed record RuleFamilyQueryResult(FirewallAddressFamily AddressFamily, IReadOnlyList<RuleQueryRow> Rows, int TotalCount)
{
    public IReadOnlyList<RuleRowProjection> VisibleRows { get; } = Rows.Select(static result => result.Row).ToArray();
}
