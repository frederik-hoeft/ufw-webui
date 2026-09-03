using System.Text.Json;
using Ufw.Ipc.Shared.Security.Intent;

namespace Ufw.Ipc.Shared.Model.Requests.Domain;

public sealed record DeleteRuleRequest() : RequestMessage(RequestMethod.Delete, "/api/v1/rules"), ISignedIntent
{
    public int Version { get; init; } = IntentProtocol.VERSION;

    public required string DeploymentId { get; init; }

    public required string KeyId { get; init; }

    public long IssuedAtUnix { get; init; }

    public required string Nonce { get; init; }

    public required string Operation { get; init; }

    public required JsonElement Payload { get; init; }

    public required string Signature { get; init; }
}
