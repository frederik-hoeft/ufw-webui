using System.Net;

namespace Ufw.Shared.Ipc.Model.Responses;

public sealed record InternalServerErrorResponse(string? Message = null) : ErrorResponse(HttpStatusCode.InternalServerError, Message);
