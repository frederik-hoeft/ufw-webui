using System.Net;

namespace Ufw.Shared.Ipc.Model.Responses;

public sealed record RequestTimeoutResponse(string? Message = null) : ErrorResponse(HttpStatusCode.RequestTimeout, Message);
