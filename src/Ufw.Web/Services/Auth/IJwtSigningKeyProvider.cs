using Microsoft.IdentityModel.Tokens;

namespace Ufw.Web.Services.Auth;

internal interface IJwtSigningKeyProvider
{
    SecurityKey SigningKey { get; }

    string SigningAlgorithm { get; }
}
