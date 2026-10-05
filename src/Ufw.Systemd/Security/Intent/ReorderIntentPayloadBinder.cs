using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal sealed class ReorderIntentPayloadBinder(MessageJsonSerializerContext jsonContext) : IIntentPayloadBinder<ReorderRulesPayload>
{
    public IntentPayloadBindingResult<ReorderRulesPayload> Bind(ISignedIntent intent)
    {
        ReorderRulesPayload? payload = intent.Payload.Deserialize(jsonContext.ReorderRulesPayload);
        if (payload is null || string.IsNullOrWhiteSpace(payload.BaselineFingerprint) || payload.DesiredOrder is null)
        {
            return new IntentPayloadBindingResult<ReorderRulesPayload>.Rejected(new BadRequestResponse("Reorder payload must include a baseline fingerprint and desired order."));
        }

        if (!FirewallRuleSnapshotFingerprint.IsValid(payload.BaselineFingerprint))
        {
            return new IntentPayloadBindingResult<ReorderRulesPayload>.Rejected(new BadRequestResponse("Reorder baseline fingerprint is malformed."));
        }

        ReorderRulesPayload verifiedPayload = new()
        {
            BaselineFingerprint = payload.BaselineFingerprint,
            DesiredOrder = [.. payload.DesiredOrder],
        };
        return new IntentPayloadBindingResult<ReorderRulesPayload>.Accepted(verifiedPayload, IntentCanonicalizer.CanonicalizeReorder(intent, verifiedPayload));
    }
}
