using Ufw.Web.Api.V1.Models.KnownHosts;

namespace Ufw.Web.Services.KnownHosts;

public sealed record KnownHostMutationResult(KnownHostMutationOutcome Outcome, KnownHostInventoryResponse? Inventory = null);
