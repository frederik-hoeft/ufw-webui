using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Data.Model;

internal sealed partial class RuleMetadataTagEntry : IDiscoverableModelConfiguration<RuleMetadataTagEntry>
{
    public static void Configure(EntityTypeBuilder<RuleMetadataTagEntry> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("RuleMetadataTags");
        self.HasKey(static tag => tag.Id);

        self.Property(static tag => tag.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd();
        self.Property(static tag => tag.RuleMetadataId)
            .HasColumnName("RuleMetadataId")
            .IsRequired();
        self.Property(static tag => tag.Name)
            .HasColumnName("Name")
            .HasMaxLength(MAX_NAME_LENGTH)
            .IsRequired();
        self.Property(static tag => tag.NormalizedName)
            .HasColumnName("NormalizedName")
            .HasMaxLength(MAX_NAME_LENGTH)
            .IsRequired();

        self.HasOne(static tag => tag.RuleMetadata)
            .WithMany(static metadata => metadata.Tags)
            .HasForeignKey(static tag => tag.RuleMetadataId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        self.HasIndex(static tag => new { tag.RuleMetadataId, tag.NormalizedName }).IsUnique();
        self.HasIndex(static tag => tag.NormalizedName);
    }
}
