using Ufw.Shared.Firewall;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.Rules.Presentation;

internal sealed class RuleEndpointKnownHostProjectionService : IRuleEndpointKnownHostProjectionService
{
    public RuleEndpointKnownHostProjection Project(string? endpoint, IReadOnlyList<KnownHostInventoryItem> knownHosts)
    {
        ArgumentNullException.ThrowIfNull(knownHosts);
        if (!FirewallAddressValue.TryNormalizeLiteral(endpoint, out string? normalizedAddress, out FirewallAddressFamily addressFamily))
        {
            return new RuleEndpointKnownHostProjection(null, []);
        }

        KnownHostInventoryItem[] matches =
        [
            .. knownHosts.Where(host => host.AddressFamily == addressFamily && string.Equals(host.Address, normalizedAddress, StringComparison.OrdinalIgnoreCase))
        ];
        return new RuleEndpointKnownHostProjection(normalizedAddress, matches);
    }
}
