using Ufw.Shared.Management.KnownHosts;

namespace Ufw.Web.Services.KnownHosts;

public sealed record KnownHostMutationResult(KnownHostMutationOutcome Outcome, IReadOnlyList<KnownHostInventoryItem>? Inventory = null);
