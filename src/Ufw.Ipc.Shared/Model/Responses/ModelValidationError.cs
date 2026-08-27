namespace Ufw.Ipc.Shared.Model.Responses;

public sealed record ModelValidationError(string PropertyName, string ErrorMessage);
