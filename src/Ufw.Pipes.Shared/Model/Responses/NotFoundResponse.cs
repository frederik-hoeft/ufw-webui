using System.Net;

namespace Ufw.Pipes.Shared.Model.Responses;

public sealed record NotFoundResponse(string? Message = null) : ErrorResponse(HttpStatusCode.NotFound, Message);