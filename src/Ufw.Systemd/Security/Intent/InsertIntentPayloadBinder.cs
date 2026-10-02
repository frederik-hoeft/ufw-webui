using System.Text.Json;
﻿using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal sealed class InsertIntentPayloadBinder(MessageJsonSerializerContext jsonContext) : IIntentPayloadBinder<InsertRulePayload>
{
    public IntentPayloadBindingResult<InsertRulePayload> Bind(ISignedIntent intent)
    {
        InsertRulePayload? payload = intent.Payload.Deserialize(jsonContext.InsertRulePayload);
        if (payload?.Rule is null || string.IsNullOrWhiteSpace(payload.BaselineFingerprint))
        {
            return new IntentPayloadBindingResult<InsertRulePayload>.Rejected(new BadRequestResponse("Insert-rule payload must include a baseline fingerprint and rule specification."));
        }

        if (!RuleSpecificationValidator.TryValidate(payload.Rule, out ModelValidationErrorResponse? validationError))
        {
            return new IntentPayloadBindingResult<InsertRulePayload>.Rejected(validationError);
        }

        InsertRulePayload verifiedPayload = new()
        {
            BaselineFingerprint = payload.BaselineFingerprint,
            AnchorOccurrenceId = payload.AnchorOccurrenceId,
            Placement = payload.Placement,
            Rule = RuleSpecificationNormalizer.Normalize(payload.Rule),
        };
        try
        {
            RuleInsertionContract.ValidatePayload(verifiedPayload);
        }
        catch (ArgumentException exception)
        {
            return new IntentPayloadBindingResult<InsertRulePayload>.Rejected(new BadRequestResponse(exception.Message));
        }

        return new IntentPayloadBindingResult<InsertRulePayload>.Accepted(verifiedPayload, IntentCanonicalizer.CanonicalizeInsert(intent, verifiedPayload));
    }
}
