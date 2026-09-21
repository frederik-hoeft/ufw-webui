using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Services.KnownHosts;

public sealed record KnownHostMutationResult(KnownHostMutationOutcome Outcome, KnownHostInventoryResponse? Inventory = null);
