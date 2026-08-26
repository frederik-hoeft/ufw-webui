using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Data;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(entity =>
        {
            entity.Property(static token => token.TokenHash).HasMaxLength(64);
            entity.Property(static token => token.ReplacedByTokenHash).HasMaxLength(64);
            entity.Property(static token => token.ConcurrencyToken).HasMaxLength(32).IsConcurrencyToken();

            entity.HasIndex(static token => token.TokenHash).IsUnique();
            entity.HasIndex(static token => token.FamilyId);
            entity.HasIndex(static token => token.ExpiresAt);

            entity.HasOne(static token => token.User)
                .WithMany()
                .HasForeignKey(static token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
