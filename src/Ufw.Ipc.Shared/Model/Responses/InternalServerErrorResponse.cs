using System.Net;

namespace Ufw.Ipc.Shared.Model.Responses;

public sealed record InternalServerErrorResponse(string? Message = null) : ErrorResponse(HttpStatusCode.InternalServerError, Message);
