using Ufw.Web.Api.V1.Models.Rules;

namespace Ufw.Web.Services.Rules;

public sealed record RuleTagMutationResult(RuleTagMutationOutcome Outcome, RuleTagInventoryResponse? Inventory = null);
