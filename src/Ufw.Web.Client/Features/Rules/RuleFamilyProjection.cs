using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules;

public sealed record RuleFamilyProjection
{
    public RuleFamilyProjection(FirewallAddressFamily addressFamily, IReadOnlyList<RuleRowProjection> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        RuleRowProjection[] familyRows = rows.ToArray();
        if (familyRows.Any(row => row.AddressFamily != addressFamily))
        {
            throw new ArgumentException("Every projected rule must belong to the projected address family.", nameof(rows));
        }

        AddressFamily = addressFamily;
        Rows = familyRows;
    }

    public FirewallAddressFamily AddressFamily { get; }

    public IReadOnlyList<RuleRowProjection> Rows { get; }
}
