using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Data.Model;

internal sealed partial class RefreshToken : IDiscoverableModelConfiguration<RefreshToken>
{
    public static void Configure(EntityTypeBuilder<RefreshToken> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("RefreshTokens");
        self.HasKey(static token => token.Id);

        self.Property(static token => token.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd();
        self.Property(static token => token.UserId)
            .HasColumnName("UserId")
            .IsRequired();
        self.Property(static token => token.TokenHash)
            .HasColumnName("TokenHash")
            .HasMaxLength(64)
            .IsRequired();
        self.Property(static token => token.FamilyId)
            .HasColumnName("FamilyId")
            .IsRequired();
        self.Property(static token => token.SecurityStamp)
            .HasColumnName("SecurityStamp");
        self.Property(static token => token.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();
        self.Property(static token => token.ExpiresAt)
            .HasColumnName("ExpiresAt")
            .IsRequired();
        self.Property(static token => token.RevokedAt)
            .HasColumnName("RevokedAt");
        self.Property(static token => token.ReplacedByTokenHash)
            .HasColumnName("ReplacedByTokenHash")
            .HasMaxLength(64);
        self.Property(static token => token.ConcurrencyToken)
            .HasColumnName("ConcurrencyToken")
            .HasMaxLength(32)
            .IsConcurrencyToken()
            .IsRequired();

        self.HasIndex(static token => token.TokenHash).IsUnique();
        self.HasIndex(static token => token.FamilyId);
        self.HasIndex(static token => token.ExpiresAt);

        self.HasOne(static token => token.User)
            .WithMany()
            .HasForeignKey(static token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
