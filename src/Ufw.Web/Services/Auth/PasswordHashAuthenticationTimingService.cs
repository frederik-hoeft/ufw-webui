using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Ufw.Web.Services.Auth;

internal sealed class PasswordHashAuthenticationTimingService : IAuthenticationTimingService
{
    private readonly IdentityUser _dummyUser = new() { Id = Guid.NewGuid().ToString(), UserName = "invalid-user" };
    private readonly PasswordHasher<IdentityUser> _passwordHasher;
    private readonly string _dummyPasswordHash;

    public PasswordHashAuthenticationTimingService(IOptions<PasswordHasherOptions> options)
    {
        _passwordHasher = new PasswordHasher<IdentityUser>(options);
        _dummyPasswordHash = _passwordHasher.HashPassword(_dummyUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public void PerformDummyPasswordVerification(string suppliedPassword) =>
        _ = _passwordHasher.VerifyHashedPassword(_dummyUser, _dummyPasswordHash, suppliedPassword);
}
