using System.Net;

namespace Ufw.Ipc.Shared.Model.Responses;

public sealed record NotImplementedResponse(string? Message = null) : ErrorResponse(HttpStatusCode.NotImplemented, Message);
