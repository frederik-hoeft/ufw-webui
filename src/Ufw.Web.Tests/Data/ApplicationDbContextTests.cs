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

    [TestMethod]
    public void PostgreSqlModel_RuleGroupsMigrationIsDiscoverable()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=ufw_webui;Username=ufw_webui;Password=ufw_webui")
            .Options;
        using ApplicationDbContext context = new(options, new ApplicationModelLoader());

        CollectionAssert.Contains(context.Database.GetMigrations().ToArray(), "20260926111030_RuleGroupPersistence");

        RuleGroupPersistence migration = new();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? group = migration.TargetModel.FindEntityType("Ufw.Web.Data.Model.RuleGroupEntry");
        Assert.IsNotNull(group);
        Assert.AreEqual("citext", group.FindProperty("Name")?.GetColumnType());
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? metadata = migration.TargetModel.FindEntityType("Ufw.Web.Data.Model.RuleMetadataEntry");
        Assert.IsNotNull(metadata);
        Assert.IsNotNull(metadata.FindProperty("GroupId"));
        Assert.AreEqual(DeleteBehavior.Restrict, metadata.GetForeignKeys().Single(foreignKey => foreignKey.Properties.Any(property => property.Name == "GroupId")).DeleteBehavior);
    }
}
