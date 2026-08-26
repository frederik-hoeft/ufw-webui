using Microsoft.AspNetCore.Identity;

namespace Ufw.Web.Data.Model;

internal sealed class RefreshToken
{
    public long Id { get; set; }

    public required string UserId { get; set; }

    public IdentityUser User { get; set; } = null!;

    public required string TokenHash { get; set; }

    public Guid FamilyId { get; set; }

    public string? SecurityStamp { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString("N");
}
