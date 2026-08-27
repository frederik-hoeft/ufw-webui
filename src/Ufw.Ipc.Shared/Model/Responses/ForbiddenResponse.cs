using System.Net;

namespace Ufw.Ipc.Shared.Model.Responses;

public sealed record ForbiddenResponse(string? Message = null) : ErrorResponse(HttpStatusCode.Forbidden, Message);
