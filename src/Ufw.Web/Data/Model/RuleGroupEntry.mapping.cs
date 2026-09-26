using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Data.Model;

internal sealed partial class RuleGroupEntry : IDiscoverableModelConfiguration<RuleGroupEntry>
{
    public static void Configure(EntityTypeBuilder<RuleGroupEntry> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("RuleGroups");
        self.HasKey(static group => group.Id);

        self.Property(static group => group.Id)
            .HasColumnName("Id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();
        self.Property(static group => group.PublicId)
            .HasColumnName("PublicId")
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();
        self.Property(static group => group.Name)
            .HasColumnName("Name")
            .HasColumnType("citext")
            .HasMaxLength(MAX_NAME_LENGTH)
            .IsRequired();
        self.Property(static group => group.Comment)
            .HasColumnName("Comment")
            .HasColumnType("character varying(4000)")
            .HasMaxLength(MAX_COMMENT_LENGTH);

        self.HasIndex(static group => group.PublicId).IsUnique();
        self.HasIndex(static group => group.Name).IsUnique();
    }
}
