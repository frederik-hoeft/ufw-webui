using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using Ufw.Web.Configuration;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Services.Auth;

internal sealed class RefreshTokenService(ApplicationDbContext context, IOptions<RefreshTokenOptions> options, TimeProvider timeProvider) : IRefreshTokenService
{
    private readonly RefreshTokenOptions _options = options.Value;

    public async Task<RefreshTokenIssueResult> IssueAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        DateTimeOffset now = timeProvider.GetUtcNow();
        string token = GenerateToken();
        DateTimeOffset expiresAt = now.Add(_options.Lifetime);

        context.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(token),
            FamilyId = Guid.CreateVersion7(),
            SecurityStamp = user.SecurityStamp,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        });

        await context.SaveChangesAsync(cancellationToken);
        return new RefreshTokenIssueResult(token, expiresAt);
    }

    public async Task<RefreshTokenRotationResult?> RotateAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string tokenHash = HashToken(token);
        DateTimeOffset now = timeProvider.GetUtcNow();
        RefreshToken? current = await context.Set<RefreshToken>()
            .Include(static refreshToken => refreshToken.User)
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

        if (current is null)
        {
            return null;
        }

        if (!string.Equals(current.SecurityStamp, current.User.SecurityStamp, StringComparison.Ordinal)
            || current.RevokedAt is not null
            || current.ExpiresAt <= now)
        {
            // Invalidating the whole active family is deliberate. It keeps replay, expiry,
            // and changed-identity state conservative and makes this path robust when a
            // sibling request is rotating the same family concurrently.
            context.ChangeTracker.Clear();
            await RevokeActiveFamilyTokensBulkAsync(current.FamilyId, now, cancellationToken);
            return null;
        }

        string replacementToken = GenerateToken();
        string replacementHash = HashToken(replacementToken);
        DateTimeOffset replacementExpiresAt = now.Add(_options.Lifetime);

        current.RevokedAt = now;
        current.ReplacedByTokenHash = replacementHash;
        current.ConcurrencyToken = Guid.NewGuid().ToString("N");

        context.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = current.UserId,
            TokenHash = replacementHash,
            FamilyId = current.FamilyId,
            SecurityStamp = current.SecurityStamp,
            CreatedAt = now,
            ExpiresAt = replacementExpiresAt,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request consumed this token after it was read. Clear stale tracked
            // state, then invalidate whatever token is now active in the family. With the
            // request transaction at ReadCommitted this update observes the winning commit.
            context.ChangeTracker.Clear();
            await RevokeActiveFamilyTokensBulkAsync(current.FamilyId, now, cancellationToken);
            return null;
        }

        return new RefreshTokenRotationResult(current.User, replacementToken, replacementExpiresAt);
    }

    public async Task RevokeFamilyAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string tokenHash = HashToken(token);
        RefreshToken? current = await context.Set<RefreshToken>()
            .AsNoTracking()
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);
        if (current is null)
        {
            return;
        }

        await RevokeActiveFamilyTokensBulkAsync(current.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
    }

    private async Task RevokeActiveFamilyTokensBulkAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        string concurrencyToken = Guid.NewGuid().ToString("N");
        await context.Set<RefreshToken>()
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, (DateTimeOffset?)revokedAt)
                .SetProperty(token => token.ConcurrencyToken, concurrencyToken), cancellationToken);
    }

    private static string GenerateToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
