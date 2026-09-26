using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Services.Rules;

public sealed record RuleGroupMutationResult(RuleGroupMutationOutcome Outcome, RuleGroupInventoryResponse? Inventory = null);
