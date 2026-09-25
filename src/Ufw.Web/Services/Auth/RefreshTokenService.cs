using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using Ufw.Web.Configuration;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Services.Auth;

internal sealed class RefreshTokenService(ITransactionServiceHandle transactionService, IOptions<RefreshTokenOptions> options, TimeProvider timeProvider)
    : DatabaseService<ApplicationDbContext>(transactionService), IRefreshTokenService
{
    private readonly RefreshTokenOptions _options = options.Value;

    public Task<RefreshTokenIssueResult> IssueAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Transaction.Scoped.RunAsync<RefreshTokenIssueResult>(async (context, transaction, ct) =>
        {
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

            await context.SaveChangesAsync(ct);
            return transaction.Commit(new RefreshTokenIssueResult(token, expiresAt));
        }, cancellationToken);
    }

    public Task<RefreshTokenRotationResult?> RotateAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return Transaction.Scoped.RunAsync<RefreshTokenRotationResult?>(async (context, transaction, ct) =>
        {
            string tokenHash = HashToken(token);
            DateTimeOffset now = timeProvider.GetUtcNow();
            RefreshToken? current = await context.Set<RefreshToken>()
                .Include(static refreshToken => refreshToken.User)
                .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, ct);

            if (current is null)
            {
                return transaction.Rollback<RefreshTokenRotationResult?>(null);
            }

            if (!string.Equals(current.SecurityStamp, current.User.SecurityStamp, StringComparison.Ordinal)
                || current.RevokedAt is not null
                || current.ExpiresAt <= now)
            {
                // Invalidating the whole active family is deliberate. It keeps replay, expiry,
                // and changed-identity state conservative and makes this path robust when a
                // sibling request is rotating the same family concurrently.
                context.ChangeTracker.Clear();
                await RevokeActiveFamilyTokensBulkAsync(context, current.FamilyId, now, ct);
                return transaction.Commit<RefreshTokenRotationResult?>(null);
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
                await context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request consumed this token after it was read. Clear stale tracked
                // state, then invalidate whatever token is now active in the family. With the
                // request transaction at ReadCommitted this update observes the winning commit.
                context.ChangeTracker.Clear();
                await RevokeActiveFamilyTokensBulkAsync(context, current.FamilyId, now, ct);
                return transaction.Commit<RefreshTokenRotationResult?>(null);
            }

            return transaction.Commit<RefreshTokenRotationResult?>(new RefreshTokenRotationResult(current.User, replacementToken, replacementExpiresAt));
        }, cancellationToken);
    }

    public Task RevokeFamilyAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return Transaction.Scoped.RunAsync(async (context, transaction, ct) =>
        {
            string tokenHash = HashToken(token);
            RefreshToken? current = await context.Set<RefreshToken>()
                .AsNoTracking()
                .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, ct);
            if (current is null)
            {
                // Revocation is idempotent. A missing token is a successful no-op and must not
                // force an enclosing transaction to roll back unrelated work.
                return transaction.Commit();
            }

            await RevokeActiveFamilyTokensBulkAsync(context, current.FamilyId, timeProvider.GetUtcNow(), ct);
            return transaction.Commit();
        }, cancellationToken);
    }

    public Task RevokeUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return Transaction.Scoped.RunAsync(async (context, transaction, ct) =>
        {
            DateTimeOffset revokedAt = timeProvider.GetUtcNow();
            string concurrencyToken = Guid.NewGuid().ToString("N");
            await context.Set<RefreshToken>()
                .Where(token => token.UserId == userId && token.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(token => token.RevokedAt, (DateTimeOffset?)revokedAt)
                    .SetProperty(token => token.ConcurrencyToken, concurrencyToken), ct);
            return transaction.Commit();
        }, cancellationToken);
    }

    private static async Task RevokeActiveFamilyTokensBulkAsync(
        ApplicationDbContext context,
        Guid familyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
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
