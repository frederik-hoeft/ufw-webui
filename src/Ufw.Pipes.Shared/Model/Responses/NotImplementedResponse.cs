using System.Net;

namespace Ufw.Pipes.Shared.Model.Responses;

public sealed record NotImplementedResponse(string? Message = null) : ErrorResponse(HttpStatusCode.NotImplemented, Message);
