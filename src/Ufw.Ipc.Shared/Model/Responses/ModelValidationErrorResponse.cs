namespace Ufw.Ipc.Shared.Model.Responses;

public sealed record ModelValidationErrorResponse(ModelValidationError[] Errors) : BadRequestResponse("One or more validation errors occurred.");