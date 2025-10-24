using System.Net;

namespace Ufw.Pipes.Shared.Model.Responses;

public record ErrorResponse(HttpStatusCode StatusCode, string? Message) : ResponseMessage(StatusCode);
