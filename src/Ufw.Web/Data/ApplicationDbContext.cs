using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wkg.EntityFrameworkCore.Configuration;
using Wkg.EntityFrameworkCore.Configuration.Policies.Defaults.EntityNamingPolicies;
using Wkg.EntityFrameworkCore.Configuration.Policies.Defaults.PropertyMappingPolicies;
using Wkg.EntityFrameworkCore.Extensions;

namespace Ufw.Web.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IModelLoader modelLoader) : IdentityDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);
        builder.LoadModels(modelLoader, options => options
            .ConfigurePolicies(policies => policies
                .AddPolicy<EntityNaming>(policy => policy.RequireExplicit())
                .AddPolicy<PropertyMapping>(policy => policy.RequireExplicit())));
    }
}
