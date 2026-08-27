using System.Net;

namespace Ufw.Ipc.Shared.Model.Responses;

public sealed record UnprocessableContentResponse(string? Message = null) : ErrorResponse(HttpStatusCode.UnprocessableContent, Message);
