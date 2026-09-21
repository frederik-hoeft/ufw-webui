using Ufw.Shared.Firewall;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.Rules.Filtering;

internal sealed record RuleFilterContext(FirewallAddressFamily AddressFamily, IReadOnlyList<KnownHostInventoryItem> KnownHosts);
