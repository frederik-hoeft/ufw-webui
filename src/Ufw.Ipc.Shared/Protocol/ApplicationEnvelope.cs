using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ufw.Ipc.Shared.Protocol;

/// <summary>
/// On-wire application document. Required fields are annotated so an empty object
/// cannot deserialize into a valid envelope.
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

    public JsonElement? Payload { get; init; }
}
