using System.Net;
using System.Text.Json.Serialization;
using Ufw.Roslyn.Controllers;

namespace Ufw.Ipc.Shared.Model.Responses;

public abstract record ResponseMessage : IResponseMessage, IMessagePayload, IIdentifiable
{
    private readonly HttpStatusCode _statusCode;

    private protected ResponseMessage(HttpStatusCode statusCode)
    {
        _statusCode = statusCode;
        Id = $"{(int)statusCode}";
    }

    [JsonIgnore]
    public string Id { get; }

    public string? Method => null;

    HttpStatusCode IResponseMessage.StatusCode => _statusCode;
}
