using Microsoft.AspNetCore.Identity;

namespace Ufw.Web.Services.Auth;

public sealed record PasswordChangeResult(IdentityResult IdentityResult, AuthenticationTokenResult? Authentication);
