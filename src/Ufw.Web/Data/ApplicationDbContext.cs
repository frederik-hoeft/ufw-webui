using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ufw.Web.Models;

namespace Ufw.Web.Data;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<UfwRule> UfwRules { get; set; }
}
