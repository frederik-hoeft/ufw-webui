using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ufw.Web.Configuration;

namespace Ufw.Web.Services.Auth;

internal sealed class ECDsaJwtSigningKeyProvider : IJwtSigningKeyProvider
{
    public ECDsaJwtSigningKeyProvider(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string pem = File.ReadAllText(options.Value.SigningKeyPath);
        using ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        SigningKey = new ECDsaSecurityKey(ecdsa);
    }

    public SecurityKey SigningKey { get; }
}