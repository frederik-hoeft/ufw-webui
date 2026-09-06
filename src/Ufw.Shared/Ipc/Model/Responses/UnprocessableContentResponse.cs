using System.Net;

namespace Ufw.Shared.Ipc.Model.Responses;

public sealed record UnprocessableContentResponse(string? Message = null) : ErrorResponse(HttpStatusCode.UnprocessableContent, Message);
