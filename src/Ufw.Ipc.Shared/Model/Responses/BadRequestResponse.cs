using System.Net;

namespace Ufw.Ipc.Shared.Model.Responses;

public record BadRequestResponse(string? Message = null) : ErrorResponse(HttpStatusCode.BadRequest, Message);
