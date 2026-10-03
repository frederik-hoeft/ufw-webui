using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ufw.Shared.Security.Intent;

namespace Ufw.Web.Model.V1.Rules.Intent;

/// <summary>
/// HTTP-facing signed mutation envelope. Validation inspects the exact signed values received from the client; it never normalizes or replaces them.
/// </summary>
public abstract record SignedRuleIntentRequest : ISignedIntent, IValidatableObject
{
    [JsonRequired]
    [Range(IntentProtocol.VERSION, IntentProtocol.VERSION)]
    public int Version { get; init; } = IntentProtocol.VERSION;

    [Required]
    public required string DeploymentId { get; init; }

    [Required]
    public required string KeyId { get; init; }

    [JsonRequired]
    public long IssuedAtUnix { get; init; }

    [Required]
    public required string Nonce { get; init; }

    [Required]
    public required string Operation { get; init; }

    public required JsonElement Payload { get; init; }

    [Required]
    public required string Signature { get; init; }

    protected abstract string ExpectedOperation { get; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.Equals(Operation, ExpectedOperation, StringComparison.Ordinal))
        {
            yield return new ValidationResult($"Operation must be '{ExpectedOperation}'.", [nameof(Operation)]);
        }

        foreach (ValidationResult result in SignedRuleIntentRequestValidator.ValidateEnvelope(this))
        {
            yield return result;
        }

        foreach (ValidationResult result in ValidatePayload())
        {
            yield return result;
        }
    }

    protected abstract IEnumerable<ValidationResult> ValidatePayload();
}
