using Microsoft.EntityFrameworkCore;
using Ufw.Web.Data;
using Ufw.Web.Data.Migrations;

namespace Ufw.Web.Tests.Data;

[TestClass]
public sealed class ApplicationDbContextTests
{
    [TestMethod]
    public void PostgreSqlModel_MatchesMigrationSnapshot()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=ufw_webui;Username=ufw_webui;Password=ufw_webui")
            .Options;
        using ApplicationDbContext context = new(options, new ApplicationModelLoader());

        Assert.IsFalse(context.Database.HasPendingModelChanges());
    }

    [TestMethod]
    public void PostgreSqlModel_KnownHostDnsMigrationIsDiscoverable()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=ufw_webui;Username=ufw_webui;Password=ufw_webui")
            .Options;
        using ApplicationDbContext context = new(options, new ApplicationModelLoader());

        CollectionAssert.Contains(context.Database.GetMigrations().ToArray(), "20260925160000_KnownHostDnsResolution");

        KnownHostDnsResolution migration = new();
        Assert.IsNotNull(migration.TargetModel.FindEntityType("Ufw.Web.Data.Model.KnownHostEntry")?.FindProperty("AddressSource"));
        Assert.IsNotNull(migration.TargetModel.FindEntityType("Ufw.Web.Data.Model.KnownHostEntry")?.FindProperty("DnsResolvedAt"));
    }
}
