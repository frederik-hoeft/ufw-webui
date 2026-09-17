using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Data.Model;

internal sealed partial class RuleTagEntry : IDiscoverableModelConfiguration<RuleTagEntry>
{
    public static void Configure(EntityTypeBuilder<RuleTagEntry> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("RuleTags");
        self.HasKey(static tag => tag.Id);

        self.Property(static tag => tag.Id)
            .HasColumnName("Id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();
        self.Property(static tag => tag.PublicId)
            .HasColumnName("PublicId")
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();
        self.Property(static tag => tag.Name)
            .HasColumnName("Name")
            .HasColumnType("citext")
            .HasMaxLength(MAX_NAME_LENGTH)
            .IsRequired();
        self.Property(static tag => tag.Color)
            .HasColumnName("Color")
            .HasColumnType("character(7)")
            .HasMaxLength(COLOR_LENGTH)
            .IsFixedLength()
            .IsRequired();

        self.HasIndex(static tag => tag.PublicId).IsUnique();
        self.HasIndex(static tag => tag.Name).IsUnique();
    }
}
