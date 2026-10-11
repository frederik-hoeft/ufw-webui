using Ufw.Shared.Management.KnownHosts;

namespace Ufw.Web.Client.Features.Rules;

internal sealed record RuleListRefreshResult(RuleInventoryState State, IReadOnlyList<KnownHostInventoryItem> KnownHosts);
