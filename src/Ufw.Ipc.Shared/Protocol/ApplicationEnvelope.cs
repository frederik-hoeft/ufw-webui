using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ufw.Ipc.Shared.Protocol;

/// <summary>
/// On-wire application document. Required fields are annotated so an empty object
/// cannot deserialize into a valid envelope. An undefined <see cref="JsonElement"/>
/// means that the payload property was absent; JSON null remains a distinct present value.
/// </summary>
public sealed class ApplicationEnvelope
{
    [JsonRequired]
    public int ProtocolVersion { get; init; }

    [JsonRequired]
    public string Kind { get; init; } = null!;

    public string? Method { get; init; }

    public string? Route { get; init; }

    public int? Status { get; init; }

    [JsonRequired]
    public string PayloadType { get; init; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Payload { get; init; }
}
