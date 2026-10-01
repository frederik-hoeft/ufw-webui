using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Data.Model;

internal sealed partial class RuleTemplateTagEntry : IDiscoverableModelConfiguration<RuleTemplateTagEntry>
{
    public static void Configure(EntityTypeBuilder<RuleTemplateTagEntry> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("RuleTemplateTags");
        self.HasKey(static relation => relation.Id);

        self.Property(static relation => relation.Id).HasColumnName("Id").HasColumnType("bigint").ValueGeneratedOnAdd();
        self.Property(static relation => relation.RuleTemplateId).HasColumnName("RuleTemplateId").HasColumnType("bigint").IsRequired();
        self.Property(static relation => relation.TagId).HasColumnName("TagId").HasColumnType("bigint").IsRequired();

        self.HasOne(static relation => relation.RuleTemplate)
            .WithMany(static template => template.Tags)
            .HasForeignKey(static relation => relation.RuleTemplateId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        self.HasOne(static relation => relation.Tag)
            .WithMany(static tag => tag.RuleTemplates)
            .HasForeignKey(static relation => relation.TagId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        self.HasIndex(static relation => new { relation.RuleTemplateId, relation.TagId }).IsUnique();
        self.HasIndex(static relation => relation.TagId);
    }
}
