using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Ufw.Web.Configuration;

namespace Ufw.Web.Services.Auth;

internal sealed class ECDsaJwtSigningKeyProvider : IJwtSigningKeyProvider, IDisposable
{
    private readonly ECDsa _ecdsa;

    public ECDsaJwtSigningKeyProvider(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _ecdsa = LoadSigningKey(options.Value.SigningKeyPath);
        SigningKey = new ECDsaSecurityKey(_ecdsa);
    }

    public SecurityKey SigningKey { get; }

    public string SigningAlgorithm => SecurityAlgorithms.EcdsaSha256;

    public void Dispose() => _ecdsa.Dispose();

    private static ECDsa LoadSigningKey(string path)
    {
        ECDsa ecdsa = ECDsa.Create();
        try
        {
            string pem = File.ReadAllText(path);
            ecdsa.ImportFromPem(pem);
            return ecdsa;
        }
        catch
        {
            ecdsa.Dispose();
            throw;
        }
    }
}
