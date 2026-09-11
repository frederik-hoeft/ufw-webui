using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.Auth;
using Ufw.Web.Tests.Integration.Support;

namespace Ufw.Web.Tests.Integration.Services.Auth;

[TestClass]
public sealed class AuthenticationFlowServiceIntegrationTests : ComponentIntegrationTest<AuthenticationFlowService>
{
    private const string EMAIL = "operator@example.invalid";
    private const string PASSWORD = "correct-password";

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task LoginRefreshAndReplayAsync_ReplayRevokesTheRotatedRefreshTokenFamilyAsync() =>
        UsingComponentAsync(async (service, serviceProvider, cancellationToken) =>
        {
            IntegrationTimeProvider timeProvider = serviceProvider.GetRequiredService<IntegrationTimeProvider>();
            timeProvider.SetUtcNow(new DateTimeOffset(2026, 9, 11, 17, 0, 0, TimeSpan.Zero));

            UserManager<IdentityUser> userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
            IdentityUser user = new()
            {
                UserName = EMAIL,
                Email = EMAIL,
                EmailConfirmed = true,
                LockoutEnabled = true,
            };
            IdentityResult creationResult = await userManager.CreateAsync(user, PASSWORD);
            Assert.IsTrue(creationResult.Succeeded, string.Join("; ", creationResult.Errors.Select(static error => error.Description)));

            AuthenticationTokenResult? login = await service.LoginAsync(EMAIL, PASSWORD, cancellationToken);
            Assert.IsNotNull(login);
            Assert.IsFalse(string.IsNullOrWhiteSpace(login.AccessToken.Value));
            Assert.IsFalse(string.IsNullOrWhiteSpace(login.RefreshToken));

            timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(1));
            AuthenticationTokenResult? refreshed = await service.RefreshAsync(login.RefreshToken, cancellationToken);
            Assert.IsNotNull(refreshed);
            Assert.AreNotEqual(login.RefreshToken, refreshed.RefreshToken);

            AuthenticationTokenResult? replay = await service.RefreshAsync(login.RefreshToken, cancellationToken);
            Assert.IsNull(replay);

            AuthenticationTokenResult? replacementAfterReplay = await service.RefreshAsync(refreshed.RefreshToken, cancellationToken);
            Assert.IsNull(replacementAfterReplay);

            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChangeTracker.Clear();
            int activeTokens = await context.Set<RefreshToken>()
                .CountAsync(static token => token.RevokedAt == null, cancellationToken);
            Assert.AreEqual(0, activeTokens);
        }, TestContext.CancellationToken);
}
