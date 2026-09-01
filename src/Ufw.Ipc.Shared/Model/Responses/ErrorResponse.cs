using System.Net;
using System.Text.Json.Serialization;

namespace Ufw.Ipc.Shared.Model.Responses;

public record ErrorResponse([property: JsonIgnore] HttpStatusCode StatusCode, string? Message) : ResponseMessage(StatusCode);
