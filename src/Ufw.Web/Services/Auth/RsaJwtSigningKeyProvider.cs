using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Ufw.Web.Configuration;

namespace Ufw.Web.Services.Auth;

internal sealed class RsaJwtSigningKeyProvider : IJwtSigningKeyProvider
{
    public RsaJwtSigningKeyProvider(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string pem = File.ReadAllText(options.Value.SigningKeyPath);
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        SigningKey = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true));
    }

    public SecurityKey SigningKey { get; }

    public string SigningAlgorithm => SecurityAlgorithms.RsaSha256;
}
