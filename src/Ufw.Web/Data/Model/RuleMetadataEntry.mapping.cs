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
            .ValueGeneratedOnAdd();
        self.Property(static metadata => metadata.RuleId)
            .HasColumnName("RuleId")
            .HasMaxLength(MAX_RULE_ID_LENGTH)
            .IsRequired();
        self.Property(static metadata => metadata.Group)
            .HasColumnName("Group")
            .HasMaxLength(MAX_GROUP_LENGTH);
        self.Property(static metadata => metadata.Notes)
            .HasColumnName("Notes")
            .HasMaxLength(MAX_NOTES_LENGTH);

        self.HasIndex(static metadata => metadata.RuleId).IsUnique();
    }
}
