using Microsoft.EntityFrameworkCore;
using Ufw.Web.Data;

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
}
