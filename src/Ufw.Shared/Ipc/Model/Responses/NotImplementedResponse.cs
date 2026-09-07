using System.Net;

namespace Ufw.Shared.Ipc.Model.Responses;

public sealed record NotImplementedResponse(string? Message = null) : ErrorResponse(HttpStatusCode.NotImplemented, Message);
