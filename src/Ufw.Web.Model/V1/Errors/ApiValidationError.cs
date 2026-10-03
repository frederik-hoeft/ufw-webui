namespace Ufw.Web.Model.V1.Errors;

public sealed record ApiValidationError(string PropertyName, string? Code, string Message);
