using System.Net;

namespace Ufw.Pipes.Shared.Model.Responses;

public sealed record UnprocessableContentResponse(string? Message = null) : ErrorResponse(HttpStatusCode.UnprocessableContent, Message);