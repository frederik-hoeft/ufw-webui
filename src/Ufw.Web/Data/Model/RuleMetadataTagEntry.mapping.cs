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
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();
        self.Property(static tag => tag.RuleMetadataId)
            .HasColumnName("RuleMetadataId")
            .HasColumnType("bigint")
            .IsRequired();
        self.Property(static tag => tag.TagId)
            .HasColumnName("TagId")
            .HasColumnType("bigint")
            .IsRequired();

        self.HasOne(static tag => tag.RuleMetadata)
            .WithMany(static metadata => metadata.Tags)
            .HasForeignKey(static tag => tag.RuleMetadataId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        self.HasOne(static tag => tag.Tag)
            .WithMany(static tag => tag.RuleMetadata)
            .HasForeignKey(static tag => tag.TagId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        self.HasIndex(static tag => new { tag.RuleMetadataId, tag.TagId }).IsUnique();
        self.HasIndex(static tag => tag.TagId);
    }
}
