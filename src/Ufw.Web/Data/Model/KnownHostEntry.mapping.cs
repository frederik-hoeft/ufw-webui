using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Data.Model;

internal sealed partial class KnownHostEntry : IDiscoverableModelConfiguration<KnownHostEntry>
{
    public static void Configure(EntityTypeBuilder<KnownHostEntry> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("KnownHosts");
        self.HasKey(static host => host.Id);

        self.Property(static host => host.Id)
            .HasColumnName("Id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();
        self.Property(static host => host.PublicId)
            .HasColumnName("PublicId")
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();
        self.Property(static host => host.Name)
            .HasColumnName("Name")
            .HasColumnType("character varying(128)")
            .HasMaxLength(MAX_NAME_LENGTH)
            .IsRequired();
        self.Property(static host => host.NormalizedName)
            .HasColumnName("NormalizedName")
            .HasColumnType("character varying(128)")
            .HasMaxLength(MAX_NAME_LENGTH)
            .IsRequired();
        self.Property(static host => host.Address)
            .HasColumnName("Address")
            .HasColumnType("character varying(64)")
            .HasMaxLength(MAX_ADDRESS_LENGTH)
            .IsRequired();
        self.Property(static host => host.AddressSource)
            .HasColumnName("AddressSource")
            .HasColumnType("integer")
            .HasDefaultValue(KnownHostAddressSource.Literal)
            .IsRequired();
        self.Property(static host => host.DnsResolvedAt)
            .HasColumnName("DnsResolvedAt")
            .HasColumnType("timestamp with time zone");
        self.Property(static host => host.Comment)
            .HasColumnName("Comment")
            .HasColumnType("character varying(200)")
            .HasMaxLength(MAX_COMMENT_LENGTH);
        self.Property(static host => host.IsVisible)
            .HasColumnName("IsVisible")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        self.HasIndex(static host => host.PublicId).IsUnique();
        self.HasIndex(static host => host.NormalizedName).IsUnique();
    }
}
