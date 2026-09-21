using Ufw.Shared.Firewall;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts.Model;

namespace Ufw.Web.Client.Features.Rules.Filtering;

internal sealed record RuleFilterContext(FirewallAddressFamily AddressFamily, IReadOnlyList<KnownHostInventoryItem> KnownHosts);
