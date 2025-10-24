using System.Net;

namespace Ufw.Pipes.Shared.Model.Responses;

public sealed record RequestTimeoutResponse(string? Message = null) : ErrorResponse(HttpStatusCode.RequestTimeout, Message);
