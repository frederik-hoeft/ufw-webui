namespace Ufw.Shared.Ipc.Model.Responses;

public sealed record ModelValidationError(string PropertyName, string ErrorMessage);
