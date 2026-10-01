using Microsoft.EntityFrameworkCore;
using Ufw.Web.Data;
using Ufw.Web.Data.Migrations;
using Ufw.Web.Model.V1.RuleTemplates;

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
    [TestMethod]
    public void PostgreSqlModel_RuleTemplateMigrationIsDiscoverable()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=ufw_webui;Username=ufw_webui;Password=ufw_webui")
            .Options;
        using ApplicationDbContext context = new(options, new ApplicationModelLoader());

        CollectionAssert.Contains(context.Database.GetMigrations().ToArray(), "20260927155658_RuleTemplatePersistence");

        RuleTemplatePersistence migration = new();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? template = migration.TargetModel.FindEntityType("Ufw.Web.Data.Model.RuleTemplateEntry");
        Assert.IsNotNull(template);
        Assert.AreEqual("citext", template.FindProperty("Name")?.GetColumnType());
        Assert.IsNull(template.FindProperty("RuleId"));
        Assert.IsNull(template.FindProperty("Position"));
        Assert.IsNull(template.FindProperty("Occurrence"));
        Assert.AreEqual(DeleteBehavior.Restrict, template.GetForeignKeys().Single(foreignKey => foreignKey.Properties.Any(property => property.Name == "GroupId")).DeleteBehavior);

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? templateTag = migration.TargetModel.FindEntityType("Ufw.Web.Data.Model.RuleTemplateTagEntry");
        Assert.IsNotNull(templateTag);
        Assert.AreEqual(DeleteBehavior.Restrict, templateTag.GetForeignKeys().Single(foreignKey => foreignKey.Properties.Any(property => property.Name == "TagId")).DeleteBehavior);
    }

    [TestMethod]
    public void PostgreSqlModel_RuleTemplateDescriptionLengthMigrationIsDiscoverable()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=ufw_webui;Username=ufw_webui;Password=ufw_webui")
            .Options;
        using ApplicationDbContext context = new(options, new ApplicationModelLoader());

        CollectionAssert.Contains(context.Database.GetMigrations().ToArray(), "20260930110937_RuleTemplateDescriptionLength");

        RuleTemplateDescriptionLength migration = new();
        Microsoft.EntityFrameworkCore.Metadata.IProperty? description = migration.TargetModel.FindEntityType("Ufw.Web.Data.Model.RuleTemplateEntry")?.FindProperty("Description");
        Assert.IsNotNull(description);
        Assert.AreEqual(RuleTemplateLimits.MAX_DESCRIPTION_LENGTH, description.GetMaxLength());
        Assert.AreEqual("character varying(512)", description.GetColumnType());
    }

}
