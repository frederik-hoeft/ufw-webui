using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Services.Rules;

public sealed record RuleTemplateMutationResult(RuleTemplateMutationOutcome Outcome, RuleTemplateInventoryResponse? Inventory = null);
