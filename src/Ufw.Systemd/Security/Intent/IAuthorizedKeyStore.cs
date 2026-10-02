namespace Ufw.Systemd.Security.Intent;

internal interface IAuthorizedKeyStore
{
    AuthorizedKeyVerificationResult VerifySignature(string keyId, ReadOnlyMemory<byte> data, string signature);
}

internal enum AuthorizedKeyVerificationResult
{
    Verified,
    UnknownKey,
    InvalidSignature,
}
