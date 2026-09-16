using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Data.Model;

internal sealed partial class RuleMetadataEntry : IDiscoverableModelConfiguration<RuleMetadataEntry>
{
    public static void Configure(EntityTypeBuilder<RuleMetadataEntry> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("RuleMetadata");
        self.HasKey(static metadata => metadata.Id);

        self.Property(static metadata => metadata.Id)
            .HasColumnName("Id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();
        self.Property(static metadata => metadata.PublicId)
            .HasColumnName("PublicId")
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();
        self.Property(static metadata => metadata.RuleId)
            .HasColumnName("RuleId")
            .HasColumnType("character varying(128)")
            .HasMaxLength(MAX_RULE_ID_LENGTH)
            .IsRequired();
        self.Property(static metadata => metadata.Notes)
            .HasColumnName("Notes")
            .HasColumnType("character varying(4000)")
            .HasMaxLength(MAX_NOTES_LENGTH);

        self.HasIndex(static metadata => metadata.PublicId).IsUnique();
        self.HasIndex(static metadata => metadata.RuleId).IsUnique();
    }
}
