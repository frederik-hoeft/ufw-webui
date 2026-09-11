using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Ufw.Web.Services.Auth;

namespace Ufw.Web.Tests.Services.Auth;

[TestClass]
public sealed class PasswordHashAuthenticationTimingServiceTests
{
    [TestMethod]
    public void PerformDummyPasswordVerification_AcceptsArbitrarySuppliedPasswordWithoutExternalState()
    {
        PasswordHashAuthenticationTimingService service = new(Options.Create(new PasswordHasherOptions
        {
            IterationCount = 1000,
        }));

        service.PerformDummyPasswordVerification("definitely-not-the-dummy-password");
        service.PerformDummyPasswordVerification(string.Empty);
    }

    [TestMethod]
    public void PerformDummyPasswordVerification_NullPasswordIsRejected()
    {
        PasswordHashAuthenticationTimingService service = new(Options.Create(new PasswordHasherOptions
        {
            IterationCount = 1000,
        }));

        Assert.ThrowsExactly<ArgumentNullException>(() => service.PerformDummyPasswordVerification(null!));
    }
}
