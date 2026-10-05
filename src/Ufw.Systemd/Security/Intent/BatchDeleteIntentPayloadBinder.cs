using System.Text.Json;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal sealed class BatchDeleteIntentPayloadBinder(MessageJsonSerializerContext jsonContext) : IIntentPayloadBinder<BatchDeleteRulesPayload>
{
    public IntentPayloadBindingResult<BatchDeleteRulesPayload> Bind(ISignedIntent intent)
    {
        BatchDeleteRulesPayload? payload = intent.Payload.Deserialize(jsonContext.BatchDeleteRulesPayload);
        if (payload is null || payload.OccurrenceIds is null)
        {
            return new IntentPayloadBindingResult<BatchDeleteRulesPayload>.Rejected(new BadRequestResponse("Batch-delete payload must include a baseline fingerprint and occurrence IDs."));
        }

        BatchDeleteRulesPayload verifiedPayload = new()
        {
            BaselineFingerprint = payload.BaselineFingerprint,
            OccurrenceIds = [.. payload.OccurrenceIds],
        };
        try
        {
            RuleBatchDeleteContract.ValidatePayload(verifiedPayload);
        }
        catch (ArgumentException exception)
        {
            return new IntentPayloadBindingResult<BatchDeleteRulesPayload>.Rejected(new BadRequestResponse(exception.Message));
        }

        return new IntentPayloadBindingResult<BatchDeleteRulesPayload>.Accepted(verifiedPayload, IntentCanonicalizer.CanonicalizeBatchDelete(intent, verifiedPayload));
    }
}
