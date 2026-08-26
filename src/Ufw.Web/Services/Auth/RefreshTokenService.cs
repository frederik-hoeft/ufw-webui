using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ufw.Web.Configuration;
using Ufw.Web.Data;
using RefreshTokenEntity = Ufw.Web.Data.Model.RefreshToken;

namespace Ufw.Web.Services.Auth;

internal sealed class RefreshTokenService(
    ApplicationDbContext context,
    IOptions<RefreshTokenOptions> options,
    TimeProvider timeProvider) : IRefreshTokenService
{
    private readonly RefreshTokenOptions _options = options.Value;

    public async Task<RefreshTokenIssueResult> IssueAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        DateTimeOffset now = timeProvider.GetUtcNow();
        string token = GenerateToken();
        DateTimeOffset expiresAt = now.Add(_options.Lifetime);

        context.RefreshTokens.Add(new RefreshTokenEntity
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

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        RefreshTokenEntity? current = await context.RefreshTokens
            .Include(static refreshToken => refreshToken.User)
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (!string.Equals(current.SecurityStamp, current.User.SecurityStamp, StringComparison.Ordinal))
        {
            await RevokeActiveFamilyTokensAsync(current.FamilyId, now, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (current.RevokedAt is not null)
        {
            await RevokeActiveFamilyTokensAsync(current.FamilyId, now, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (current.ExpiresAt <= now)
        {
            current.RevokedAt = now;
            current.ConcurrencyToken = Guid.NewGuid().ToString("N");
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        string replacementToken = GenerateToken();
        string replacementHash = HashToken(replacementToken);
        DateTimeOffset replacementExpiresAt = now.Add(_options.Lifetime);

        current.RevokedAt = now;
        current.ReplacedByTokenHash = replacementHash;
        current.ConcurrencyToken = Guid.NewGuid().ToString("N");

        context.RefreshTokens.Add(new RefreshTokenEntity
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
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            await RevokeActiveFamilyTokensBulkAsync(current.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        return new RefreshTokenRotationResult(current.User, replacementToken, replacementExpiresAt);
    }

    public async Task RevokeFamilyAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string tokenHash = HashToken(token);
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        RefreshTokenEntity? current = await context.RefreshTokens
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);
        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        await RevokeActiveFamilyTokensAsync(current.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RevokeActiveFamilyTokensBulkAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        string concurrencyToken = Guid.NewGuid().ToString("N");
        await context.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, (DateTimeOffset?)revokedAt)
                .SetProperty(token => token.ConcurrencyToken, concurrencyToken), cancellationToken);
    }

    private async Task RevokeActiveFamilyTokensAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        List<RefreshTokenEntity> activeTokens = await context.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (RefreshTokenEntity activeToken in activeTokens)
        {
            activeToken.RevokedAt = revokedAt;
            activeToken.ConcurrencyToken = Guid.NewGuid().ToString("N");
        }
    }

    private static string GenerateToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
