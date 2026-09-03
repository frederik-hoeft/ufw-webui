using System.Text.Json;

namespace Ufw.Ipc.Shared.Security.Intent;

/// <summary>
/// Signed mutation envelope. The signature covers a canonical encoding of every
/// field except <see cref="Signature"/> itself.
/// </summary>
public interface ISignedIntent
{
    int Version { get; }

    string DeploymentId { get; }

    string KeyId { get; }

    long IssuedAtUnix { get; }

    string Nonce { get; }

    string Operation { get; }

    JsonElement Payload { get; }

    string Signature { get; }
}
