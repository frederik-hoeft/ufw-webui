namespace Ufw.Shared.Ipc.Model.Responses;

public sealed record ModelValidationErrorResponse(ModelValidationError[] Errors) : BadRequestResponse("One or more validation errors occurred.");
