using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal sealed class AddIntentPayloadBinder(MessageJsonSerializerContext jsonContext) : IIntentPayloadBinder<AddRulePayload>
{
    public IntentPayloadBindingResult<AddRulePayload> Bind(ISignedIntent intent)
    {
        AddRulePayload? payload = intent.Payload.Deserialize(jsonContext.AddRulePayload);
        if (payload?.Rule is null)
        {
            return new IntentPayloadBindingResult<AddRulePayload>.Rejected(new BadRequestResponse("Add-rule payload is missing a rule specification."));
        }

        if (!RuleSpecificationValidator.TryValidate(payload.Rule, out ModelValidationErrorResponse? validationError))
        {
            return new IntentPayloadBindingResult<AddRulePayload>.Rejected(validationError);
        }

        AddRulePayload normalizedPayload = new() { Rule = RuleSpecificationNormalizer.Normalize(payload.Rule) };
        return new IntentPayloadBindingResult<AddRulePayload>.Accepted(normalizedPayload, IntentCanonicalizer.CanonicalizeAdd(intent, normalizedPayload));
    }
}
