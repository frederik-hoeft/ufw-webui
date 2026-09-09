using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Data.Model;

internal sealed partial class NetworkInterfaceEntry : IDiscoverableModelConfiguration<NetworkInterfaceEntry>
{
    public static void Configure(EntityTypeBuilder<NetworkInterfaceEntry> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("NetworkInterfaces");
        self.HasKey(static networkInterface => networkInterface.Id);

        self.Property(static networkInterface => networkInterface.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd();
        self.Property(static networkInterface => networkInterface.PublicId)
            .HasColumnName("PublicId")
            .ValueGeneratedNever()
            .IsRequired();
        self.Property(static networkInterface => networkInterface.Name)
            .HasColumnName("Name")
            .HasMaxLength(MAX_NAME_LENGTH)
            .IsRequired();
        self.Property(static networkInterface => networkInterface.Comment)
            .HasColumnName("Comment")
            .HasMaxLength(MAX_COMMENT_LENGTH);
        self.Property(static networkInterface => networkInterface.IsVisible)
            .HasColumnName("IsVisible")
            .HasDefaultValue(true)
            .IsRequired();

        self.HasIndex(static networkInterface => networkInterface.PublicId).IsUnique();
        self.HasIndex(static networkInterface => networkInterface.Name).IsUnique();
    }
}
