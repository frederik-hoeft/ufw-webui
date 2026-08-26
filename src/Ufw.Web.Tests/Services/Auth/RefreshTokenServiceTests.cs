using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ufw.Web.Configuration;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.Auth;

namespace Ufw.Web.Tests.Services.Auth;

[TestClass]
public sealed class RefreshTokenServiceTests
{
    [TestMethod]
    public async Task RotateAsync_ReusedToken_RevokesActiveFamilyAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ApplicationDbContext> databaseOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ApplicationDbContext context = new(databaseOptions);
        await context.Database.EnsureCreatedAsync();

        IdentityUser user = new()
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "test-user",
            NormalizedUserName = "TEST-USER",
            Email = "test@example.invalid",
            NormalizedEmail = "TEST@EXAMPLE.INVALID",
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        RefreshTokenOptions refreshTokenOptions = new() { Lifetime = TimeSpan.FromDays(1) };
        RefreshTokenService service = new(context, Options.Create(refreshTokenOptions), TimeProvider.System);

        RefreshTokenIssueResult issued = await service.IssueAsync(user);
        RefreshToken persistedToken = await context.RefreshTokens.SingleAsync();
        Assert.AreNotEqual(issued.Token, persistedToken.TokenHash);
        Assert.AreEqual(64, persistedToken.TokenHash.Length);

        RefreshTokenRotationResult? rotation = await service.RotateAsync(issued.Token);
        Assert.IsNotNull(rotation);
        Assert.AreNotEqual(issued.Token, rotation.Token);

        RefreshTokenRotationResult? replay = await service.RotateAsync(issued.Token);
        Assert.IsNull(replay);

        context.ChangeTracker.Clear();
        int activeFamilyTokenCount = await context.RefreshTokens
            .CountAsync(token => token.FamilyId == persistedToken.FamilyId && token.RevokedAt == null);
        Assert.AreEqual(0, activeFamilyTokenCount);
    }
}
