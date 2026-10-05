using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Model.V1.Errors;

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
        foreach (ApiValidationError error in GetApiValidationErrors())
        {
            yield return new ValidationResult(error.Message, [error.PropertyName]);
        }
    }

    internal IEnumerable<ApiValidationError> GetApiValidationErrors()
    {
        if (!string.Equals(Operation, ExpectedOperation, StringComparison.Ordinal))
        {
            yield return new ApiValidationError(nameof(Operation), Code: null, $"Operation must be '{ExpectedOperation}'.");
        }

        foreach (ApiValidationError error in SignedRuleIntentRequestValidator.ValidateEnvelope(this))
        {
            yield return error;
        }

        foreach (ApiValidationError error in ValidatePayload())
        {
            yield return error;
        }
    }

    protected abstract IEnumerable<ApiValidationError> ValidatePayload();
}
