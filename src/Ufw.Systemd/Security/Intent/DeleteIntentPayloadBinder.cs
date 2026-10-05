using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal sealed class DeleteIntentPayloadBinder(MessageJsonSerializerContext jsonContext) : IIntentPayloadBinder<DeleteRulePayload>
{
    public IntentPayloadBindingResult<DeleteRulePayload> Bind(ISignedIntent intent)
    {
        DeleteRulePayload? payload = intent.Payload.Deserialize(jsonContext.DeleteRulePayload);
        if (payload?.Rule is null || string.IsNullOrWhiteSpace(payload.RuleId))
        {
            return new IntentPayloadBindingResult<DeleteRulePayload>.Rejected(new BadRequestResponse("Delete-rule payload must include ruleId and a rule specification."));
        }

        if (!RuleSpecificationValidator.TryValidate(payload.Rule, out ModelValidationErrorResponse? validationError))
        {
            return new IntentPayloadBindingResult<DeleteRulePayload>.Rejected(validationError);
        }

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(payload.Rule);
        if (normalized.AddressFamily == FirewallAddressFamily.Any)
        {
            return new IntentPayloadBindingResult<DeleteRulePayload>.Rejected(new BadRequestResponse("Delete-rule specifications must use a concrete address family from the current rule listing."));
        }

        string computed = RuleIdentity.Compute(normalized);
        if (!string.Equals(computed, payload.RuleId, StringComparison.Ordinal))
        {
            return new IntentPayloadBindingResult<DeleteRulePayload>.Rejected(new BadRequestResponse("Delete ruleId does not match the supplied rule specification."));
        }

        DeleteRulePayload normalizedPayload = new() { RuleId = payload.RuleId, Rule = normalized };
        return new IntentPayloadBindingResult<DeleteRulePayload>.Accepted(normalizedPayload, IntentCanonicalizer.CanonicalizeDelete(intent, normalizedPayload));
    }
}
