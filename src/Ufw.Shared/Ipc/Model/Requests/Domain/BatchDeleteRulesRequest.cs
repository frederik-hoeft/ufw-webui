using System.Text.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Shared.Ipc.Model.Requests.Domain;

public sealed record BatchDeleteRulesRequest() : RequestMessage(RequestMethod.Delete, "/api/v1/rules/batch"), ISignedIntent
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
