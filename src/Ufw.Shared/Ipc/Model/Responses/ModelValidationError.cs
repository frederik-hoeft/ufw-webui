namespace Ufw.Shared.Ipc.Model.Responses;

/// <summary>
/// Describes one field-specific model validation failure.
/// </summary>
/// <param name="PropertyName">The model property associated with the failure.</param>
/// <param name="ErrorMessage">Human-readable diagnostic text for presentation or fallback use.</param>
/// <param name="Code">Stable machine-readable validation identity, or <see langword="null"/> when reading a legacy payload that predates validation codes.</param>
public sealed record ModelValidationError(string PropertyName, string ErrorMessage, string? Code = null);
