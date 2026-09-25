using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.Rules.Presentation;

internal sealed record RuleEndpointKnownHostProjection(string? Address, IReadOnlyList<KnownHostInventoryItem> Hosts)
{
    public bool IsMeaningful => Address is not null;
}
