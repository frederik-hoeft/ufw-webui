using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.KnownHosts.Api;

namespace Ufw.Web.Client.Features.Rules.Filtering;

internal sealed record RuleFilterContext(FirewallAddressFamily AddressFamily, IReadOnlyList<KnownHostInventoryItem> KnownHosts);
