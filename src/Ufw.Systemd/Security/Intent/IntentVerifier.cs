using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal sealed class IntentVerifier
(
    IIntentEnvelopeVerifier envelopeVerifier,
    IIntentPayloadBinder<AddRulePayload> addPayloadBinder,
    IIntentPayloadBinder<DeleteRulePayload> deletePayloadBinder,
    IIntentPayloadBinder<BatchDeleteRulesPayload> batchDeletePayloadBinder,
    IIntentPayloadBinder<InsertRulePayload> insertPayloadBinder,
    IIntentPayloadBinder<ReorderRulesPayload> reorderPayloadBinder,
    IIntentPayloadBinder<ReplaceRulePayload> replacePayloadBinder
) : IIntentVerifier
{
    public IntentVerificationResult VerifyAdd(ISignedIntent intent) => envelopeVerifier.Verify(
        intent,
        IntentOperations.ADD_RULE,
        addPayloadBinder,
        static (verified, payload) => new IntentVerificationResult.AcceptedRuleMutation(verified.KeyId, verified.Nonce, verified.ExpiresAtUnix, payload.Rule, null));

    public IntentVerificationResult VerifyDelete(ISignedIntent intent) => envelopeVerifier.Verify(
        intent,
        IntentOperations.DELETE_RULE,
        deletePayloadBinder,
        static (verified, payload) => new IntentVerificationResult.AcceptedRuleMutation(verified.KeyId, verified.Nonce, verified.ExpiresAtUnix, payload.Rule, payload.RuleId));

    public IntentVerificationResult VerifyBatchDelete(ISignedIntent intent) => envelopeVerifier.Verify(
        intent,
        IntentOperations.DELETE_RULES_BATCH,
        batchDeletePayloadBinder,
        static (verified, payload) => new IntentVerificationResult.AcceptedBatchDelete(verified.KeyId, verified.Nonce, verified.ExpiresAtUnix, payload));

    public IntentVerificationResult VerifyInsert(ISignedIntent intent) => envelopeVerifier.Verify(
        intent,
        IntentOperations.INSERT_RULE,
        insertPayloadBinder,
        static (verified, payload) => new IntentVerificationResult.AcceptedInsertion(verified.KeyId, verified.Nonce, verified.ExpiresAtUnix, payload));

    public IntentVerificationResult VerifyReorder(ISignedIntent intent) => envelopeVerifier.Verify(
        intent,
        IntentOperations.REORDER_RULES,
        reorderPayloadBinder,
        static (verified, payload) => new IntentVerificationResult.AcceptedReorder(verified.KeyId, verified.Nonce, verified.ExpiresAtUnix, payload));

    public IntentVerificationResult VerifyReplace(ISignedIntent intent) => envelopeVerifier.Verify(
        intent,
        IntentOperations.REPLACE_RULE,
        replacePayloadBinder,
        static (verified, payload) => new IntentVerificationResult.AcceptedReplacement(verified.KeyId, verified.Nonce, verified.ExpiresAtUnix, payload));
}
