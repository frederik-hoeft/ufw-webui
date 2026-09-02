using System.Text.Json;
using Ufw.Ipc.Shared.Security.Intent;

namespace Ufw.Ipc.Shared.Model.Requests.Domain;

public sealed record AddRuleRequest : RequestMessage, ISignedIntent
{
    public AddRuleRequest() : base(RequestMethod.Post, "/api/v1/rules")
    {
    }

    public int Version { get; init; } = IntentProtocol.VERSION;

    public required string KeyId { get; init; }

    public long IssuedAtUnix { get; init; }

    public required string Nonce { get; init; }

    public required string Operation { get; init; }

    public required JsonElement Payload { get; init; }

    public required string Signature { get; init; }
}
