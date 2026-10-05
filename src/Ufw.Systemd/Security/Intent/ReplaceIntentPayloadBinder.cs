using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal sealed class ReplaceIntentPayloadBinder(MessageJsonSerializerContext jsonContext) : IIntentPayloadBinder<ReplaceRulePayload>
{
    public IntentPayloadBindingResult<ReplaceRulePayload> Bind(ISignedIntent intent)
    {
        ReplaceRulePayload? payload = intent.Payload.Deserialize(jsonContext.ReplaceRulePayload);
        if (payload?.ReplacementRule is null || string.IsNullOrWhiteSpace(payload.BaselineFingerprint) || string.IsNullOrWhiteSpace(payload.OriginalRuleId))
        {
            return new IntentPayloadBindingResult<ReplaceRulePayload>.Rejected(
                new BadRequestResponse("Replace-rule payload must include a baseline fingerprint, original rule ID, and replacement rule specification."));
        }

        if (!RuleSpecificationValidator.TryValidate(payload.ReplacementRule, out ModelValidationErrorResponse? validationError))
        {
            return new IntentPayloadBindingResult<ReplaceRulePayload>.Rejected(validationError);
        }

        ReplaceRulePayload verifiedPayload = new()
        {
            BaselineFingerprint = payload.BaselineFingerprint,
            TargetOccurrenceId = payload.TargetOccurrenceId,
            OriginalRuleId = payload.OriginalRuleId,
            ReplacementRule = RuleSpecificationNormalizer.Normalize(payload.ReplacementRule),
        };
        try
        {
            RuleReplacementContract.ValidatePayload(verifiedPayload);
        }
        catch (ArgumentException exception)
        {
            return new IntentPayloadBindingResult<ReplaceRulePayload>.Rejected(new BadRequestResponse(exception.Message));
        }

        return new IntentPayloadBindingResult<ReplaceRulePayload>.Accepted(verifiedPayload, IntentCanonicalizer.CanonicalizeReplace(intent, verifiedPayload));
    }
}
