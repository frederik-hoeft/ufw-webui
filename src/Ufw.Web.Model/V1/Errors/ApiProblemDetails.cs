using System.Text.Json.Serialization;

namespace Ufw.Web.Model.V1.Errors;

public sealed class ApiProblemDetails
{
    public const string VALIDATION_ERRORS_PROPERTY = "validationErrors";

    public string? Type { get; init; }

    public string? Title { get; init; }

    public int? Status { get; init; }

    public string? Detail { get; init; }

    public string? Code { get; init; }

    public string? Instance { get; init; }

    [JsonPropertyName(VALIDATION_ERRORS_PROPERTY)]
    public IReadOnlyList<ApiValidationError>? ValidationErrors { get; init; }
}
