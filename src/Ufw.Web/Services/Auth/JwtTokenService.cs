using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ufw.Web.Configuration;

namespace Ufw.Web.Services.Auth;

internal sealed class JwtTokenService(
    UserManager<IdentityUser> userManager,
    IJwtSigningKeyProvider signingKeyProvider,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<AccessToken> IssueAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.Add(_options.AccessTokenLifetime);
        IList<string> roles = await userManager.GetRolesAsync(user);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        ];

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, user.UserName));
        }

        claims.AddRange(roles.Select(static role => new Claim(ClaimTypes.Role, role)));

        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKeyProvider.SigningKey, SecurityAlgorithms.RsaSha256),
        };

        JwtSecurityTokenHandler handler = new();
        SecurityToken token = handler.CreateToken(descriptor);
        return new AccessToken(handler.WriteToken(token), expiresAt);
    }
}
