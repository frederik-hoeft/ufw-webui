using System.Buffers.Text;
using System.Security.Cryptography;

namespace Ufw.Ipc.Shared.Security.Intent;

/// <summary>
/// ECDSA P-256 / SHA-256 helpers for intent signatures. Signature format is
/// IEEE P1363 (r || s) so a future browser WebCrypto client can interoperate.
/// </summary>
public static class IntentSigner
{
    public static string ComputeKeyId(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        byte[] spki = key.ExportSubjectPublicKeyInfo();
        byte[] hash = SHA256.HashData(spki);
        return IntentProtocol.KEY_ID_PREFIX + Base64Url.EncodeToString(hash);
    }

    public static string Sign(ECDsa privateKey, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        byte[] signature = privateKey.SignData(
            data,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Base64Url.EncodeToString(signature);
    }

    public static bool Verify(ECDsa publicKey, ReadOnlySpan<byte> data, string signature)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        if (!TryDecodeBase64Url(signature, out byte[]? signatureBytes))
        {
            return false;
        }

        return publicKey.VerifyData(
            data,
            signatureBytes,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    public static string CreateNonce()
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(IntentProtocol.NONCE_SIZE_BYTES);
        return Base64Url.EncodeToString(nonce);
    }

    public static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            bytes = Base64Url.DecodeFromChars(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static ECDsa CreateP256()
    {
        ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key;
    }

    public static bool IsP256(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ECParameters parameters = key.ExportParameters(includePrivateParameters: false);
        return parameters.Curve.Oid.FriendlyName is "nistP256" or "ECDSA_P256" or "prime256v1" or "secp256r1"
            || string.Equals(parameters.Curve.Oid.Value, "1.2.840.10045.3.1.7", StringComparison.Ordinal);
    }
}
