using System.Net;

namespace Ufw.Ipc.Shared.Model.Responses;

public sealed record RequestTimeoutResponse(string? Message = null) : ErrorResponse(HttpStatusCode.RequestTimeout, Message);
