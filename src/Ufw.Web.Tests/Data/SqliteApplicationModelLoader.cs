using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Wkg.EntityFrameworkCore.Configuration;
using Wkg.EntityFrameworkCore.Configuration.Discovery;

namespace Ufw.Web.Tests.Data;

/// <summary>
/// Loads the production model while adapting PostgreSQL's explicit 64-bit integer store type to SQLite's
/// required <c>INTEGER</c> spelling for generated integer primary keys used by integration tests.
/// </summary>
internal sealed class SqliteApplicationModelLoader : IModelLoader
{
    private readonly ApplicationModelLoader _inner = new();

    public void LoadModels(ModelBuilder builder, IEntityDiscoveryContext discoveryContext)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(discoveryContext);

        ((IModelLoader)_inner).LoadModels(builder, discoveryContext);

        foreach (IMutableProperty property in builder.Model.GetEntityTypes().SelectMany(static entity => entity.GetProperties()))
        {
            if (property.ClrType == typeof(long) && property.GetColumnType() == "bigint")
            {
                property.SetColumnType("INTEGER");
            }
        }

        builder.Entity<RuleTagEntry>()
            .Property(static tag => tag.Name)
            .UseCollation("NOCASE");
        builder.Entity<RuleGroupEntry>()
            .Property(static group => group.Name)
            .UseCollation("NOCASE");
    }
}
