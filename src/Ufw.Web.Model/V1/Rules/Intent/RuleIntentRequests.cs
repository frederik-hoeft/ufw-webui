using Ufw.Shared.Security.Intent;
using Ufw.Web.Model.V1.Errors;

namespace Ufw.Web.Model.V1.Rules.Intent;

public sealed record AddRuleIntentRequest : SignedRuleIntentRequest
{
    protected override string ExpectedOperation => IntentOperations.ADD_RULE;

    protected override IEnumerable<ApiValidationError> ValidatePayload() => SignedRuleIntentRequestValidator.ValidateAdd(Payload);
}

public sealed record InsertRuleIntentRequest : SignedRuleIntentRequest
{
    protected override string ExpectedOperation => IntentOperations.INSERT_RULE;

    protected override IEnumerable<ApiValidationError> ValidatePayload() => SignedRuleIntentRequestValidator.ValidateInsert(Payload);
}

public sealed record ReplaceRuleIntentRequest : SignedRuleIntentRequest
{
    protected override string ExpectedOperation => IntentOperations.REPLACE_RULE;

    protected override IEnumerable<ApiValidationError> ValidatePayload() => SignedRuleIntentRequestValidator.ValidateReplace(Payload);
}

public sealed record ReorderRulesIntentRequest : SignedRuleIntentRequest
{
    protected override string ExpectedOperation => IntentOperations.REORDER_RULES;

    protected override IEnumerable<ApiValidationError> ValidatePayload() => SignedRuleIntentRequestValidator.ValidateReorder(Payload);
}

public sealed record BatchDeleteRulesIntentRequest : SignedRuleIntentRequest
{
    protected override string ExpectedOperation => IntentOperations.DELETE_RULES_BATCH;

    protected override IEnumerable<ApiValidationError> ValidatePayload() => SignedRuleIntentRequestValidator.ValidateBatchDelete(Payload);
}

public sealed record DeleteRuleIntentRequest : SignedRuleIntentRequest
{
    protected override string ExpectedOperation => IntentOperations.DELETE_RULE;

    protected override IEnumerable<ApiValidationError> ValidatePayload() => SignedRuleIntentRequestValidator.ValidateDelete(Payload);
}
