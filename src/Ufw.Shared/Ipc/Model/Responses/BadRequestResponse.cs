using System.Net;

namespace Ufw.Shared.Ipc.Model.Responses;

public record BadRequestResponse(string? Message = null) : ErrorResponse(HttpStatusCode.BadRequest, Message);
