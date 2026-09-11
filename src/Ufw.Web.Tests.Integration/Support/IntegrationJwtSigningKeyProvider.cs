using Microsoft.IdentityModel.Tokens;
using System.Text;
using Ufw.Web.Services.Auth;

namespace Ufw.Web.Tests.Integration.Support;

internal sealed class IntegrationJwtSigningKeyProvider : IJwtSigningKeyProvider
{
    private static readonly byte[] s_signingKey = Encoding.ASCII.GetBytes("ufw-webui-integration-test-signing-key-32-bytes-minimum");

    public SecurityKey SigningKey { get; } = new SymmetricSecurityKey(s_signingKey);

    public string SigningAlgorithm => SecurityAlgorithms.HmacSha256;
}
