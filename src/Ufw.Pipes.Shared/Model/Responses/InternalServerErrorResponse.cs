using System.Net;

namespace Ufw.Pipes.Shared.Model.Responses;

public sealed record InternalServerErrorResponse(string? Message = null) : ErrorResponse(HttpStatusCode.InternalServerError, Message);