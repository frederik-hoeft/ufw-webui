using Ufw.Client.Api;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering;

internal sealed record RuleFilterContext(FirewallAddressFamily AddressFamily, IReadOnlyList<KnownHostInventoryItem> KnownHosts);
