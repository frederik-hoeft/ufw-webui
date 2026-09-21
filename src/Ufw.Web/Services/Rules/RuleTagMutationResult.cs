using Ufw.Web.Model.V1.RuleTags;

namespace Ufw.Web.Services.Rules;

public sealed record RuleTagMutationResult(RuleTagMutationOutcome Outcome, RuleTagInventoryResponse? Inventory = null);
