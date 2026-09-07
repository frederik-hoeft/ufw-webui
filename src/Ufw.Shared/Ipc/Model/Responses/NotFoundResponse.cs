using System.Net;

namespace Ufw.Shared.Ipc.Model.Responses;

public sealed record NotFoundResponse(string? Message = null) : ErrorResponse(HttpStatusCode.NotFound, Message);
