namespace Ufw.Web.Data.Access.Auth;

/// <summary>
/// Persists refresh-token state inside a transaction context owned by the calling authentication workflow.
/// Implementations may flush EF changes but must not create, commit, or roll back a transaction independently of the supplied context.
/// </summary>
internal interface IRefreshTokenDataAccess
{
    Task<RefreshTokenIssueResult> IssueAsync(ApplicationDbContext context, string userId, string? securityStamp, CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult?> RotateAsync(ApplicationDbContext context, string token, CancellationToken cancellationToken = default);

    Task RevokeFamilyAsync(ApplicationDbContext context, string token, CancellationToken cancellationToken = default);

    Task RevokeUserAsync(ApplicationDbContext context, string userId, CancellationToken cancellationToken = default);
}
