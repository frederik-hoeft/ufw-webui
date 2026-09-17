using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Data.Model;

internal sealed partial class NetworkInterfaceCacheState : IDiscoverableModelConfiguration<NetworkInterfaceCacheState>
{
    public static void Configure(EntityTypeBuilder<NetworkInterfaceCacheState> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("NetworkInterfaceCacheState");
        self.HasKey(static state => state.Id);

        self.Property(static state => state.Id)
            .HasColumnName("Id")
            .HasColumnType("integer")
            .ValueGeneratedNever();
        self.Property(static state => state.ReconciledAt)
            .HasColumnName("ReconciledAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
