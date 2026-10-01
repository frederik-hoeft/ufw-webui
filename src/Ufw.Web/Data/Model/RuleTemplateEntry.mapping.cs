using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ufw.Shared.Firewall;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Data.Model;

internal sealed partial class RuleTemplateEntry : IDiscoverableModelConfiguration<RuleTemplateEntry>
{
    public static void Configure(EntityTypeBuilder<RuleTemplateEntry> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("RuleTemplates");
        self.HasKey(static template => template.Id);

        self.Property(static template => template.Id).HasColumnName("Id").HasColumnType("bigint").ValueGeneratedOnAdd();
        self.Property(static template => template.PublicId).HasColumnName("PublicId").HasColumnType("uuid").ValueGeneratedNever().IsRequired();
        self.Property(static template => template.Name).HasColumnName("Name").HasColumnType("citext").HasMaxLength(MAX_NAME_LENGTH).IsRequired();
        self.Property(static template => template.Description).HasColumnName("Description").HasColumnType("character varying(512)").HasMaxLength(MAX_DESCRIPTION_LENGTH);
        self.Property(static template => template.Action).HasColumnName("Action").HasColumnType("integer").IsRequired();
        self.Property(static template => template.AddressFamily).HasColumnName("AddressFamily").HasColumnType("integer").IsRequired();
        self.Property(static template => template.Direction).HasColumnName("Direction").HasColumnType("integer").IsRequired();
        self.Property(static template => template.Protocol).HasColumnName("Protocol").HasColumnType("integer").IsRequired();
        self.Property(static template => template.Source).HasColumnName("Source").HasColumnType("text").IsRequired();
        self.Property(static template => template.SourcePorts).HasColumnName("SourcePorts").HasColumnType("text");
        self.Property(static template => template.SourceInterface).HasColumnName("SourceInterface").HasColumnType("character varying(32)").HasMaxLength(RuleSpecificationValidator.MAX_INTERFACE_LENGTH);
        self.Property(static template => template.Destination).HasColumnName("Destination").HasColumnType("text").IsRequired();
        self.Property(static template => template.DestinationPorts).HasColumnName("DestinationPorts").HasColumnType("text");
        self.Property(static template => template.DestinationInterface).HasColumnName("DestinationInterface").HasColumnType("character varying(32)").HasMaxLength(RuleSpecificationValidator.MAX_INTERFACE_LENGTH);
        self.Property(static template => template.Comment).HasColumnName("Comment").HasColumnType("character varying(200)").HasMaxLength(RuleSpecificationValidator.MAX_COMMENT_LENGTH);
        self.Property(static template => template.Notes).HasColumnName("Notes").HasColumnType("character varying(4000)").HasMaxLength(RuleMetadataEntry.MAX_NOTES_LENGTH);
        self.Property(static template => template.GroupId).HasColumnName("GroupId").HasColumnType("bigint");

        self.HasOne(static template => template.Group)
            .WithMany(static group => group.RuleTemplates)
            .HasForeignKey(static template => template.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        self.HasIndex(static template => template.PublicId).IsUnique();
        self.HasIndex(static template => template.Name);
        self.HasIndex(static template => template.GroupId);
    }
}
