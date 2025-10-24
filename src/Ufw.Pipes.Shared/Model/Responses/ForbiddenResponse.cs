using System.Net;

namespace Ufw.Pipes.Shared.Model.Responses;

public sealed record ForbiddenResponse(string? Message = null) : ErrorResponse(HttpStatusCode.Forbidden, Message);
