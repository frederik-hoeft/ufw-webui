using System.Net;

namespace Ufw.Ipc.Shared.Model.Responses;

public record ErrorResponse(HttpStatusCode StatusCode, string? Message) : ResponseMessage(StatusCode);
