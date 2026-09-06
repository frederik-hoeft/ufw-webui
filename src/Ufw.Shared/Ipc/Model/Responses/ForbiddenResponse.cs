using System.Net;

namespace Ufw.Shared.Ipc.Model.Responses;

public sealed record ForbiddenResponse(string? Message = null) : ErrorResponse(HttpStatusCode.Forbidden, Message);
