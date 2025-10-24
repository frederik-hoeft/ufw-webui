using System.Net;

namespace Ufw.Pipes.Shared.Model.Responses;

public record BadRequestResponse(string? Message = null) : ErrorResponse(HttpStatusCode.BadRequest, Message);