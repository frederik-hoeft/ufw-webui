using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Ufw.Web.Configuration;

namespace Ufw.Web.Services.Auth;

internal sealed partial class AuthenticationBootstrapService(
    UserManager<IdentityUser> userManager,
    IOptions<AuthenticationBootstrapOptions> options,
    ILogger<AuthenticationBootstrapService> logger)
{
    private readonly AuthenticationBootstrapOptions _options = options.Value;

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        foreach (BootstrapUserOptions configuredUser in _options.Users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string email = configuredUser.Email.Trim();
            string? configuredUserName = string.IsNullOrWhiteSpace(configuredUser.UserName)
                ? null
                : configuredUser.UserName.Trim();
            string userNameForCreation = configuredUserName ?? email;

            IdentityUser? user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                IdentityUser? userNameOwner = await userManager.FindByNameAsync(userNameForCreation);
                if (userNameOwner is not null)
                {
                    if (string.Equals(userNameOwner.Email, email, StringComparison.OrdinalIgnoreCase))
                    {
                        user = userNameOwner;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Cannot create bootstrap user '{email}' because username '{userNameForCreation}' is already owned by another account.");
                    }
                }
            }

            if (user is null)
            {
                if (string.IsNullOrWhiteSpace(configuredUser.Password))
                {
                    throw new InvalidOperationException(
                        $"Bootstrap user '{email}' does not exist and requires a password for initial creation.");
                }

                IdentityUser newUser = new()
                {
                    UserName = userNameForCreation,
                    Email = email,
                    EmailConfirmed = configuredUser.EmailConfirmed,
                    LockoutEnabled = true,
                };

                IdentityResult createResult = await userManager.CreateAsync(newUser, configuredUser.Password);
                if (createResult.Succeeded)
                {
                    LogCreatedBootstrapUser(logger, email);
                    continue;
                }

                // Another application replica may have created the same account between
                // the lookup and CreateAsync. Re-read once before treating the result as a
                // real provisioning failure.
                user = await userManager.FindByEmailAsync(email);
                if (user is null)
                {
                    throw CreateIdentityException(createResult, "create", email);
                }
            }

            if (configuredUserName is not null
                && !string.Equals(user.UserName, configuredUserName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Bootstrap user '{email}' already exists with username '{user.UserName}', not configured username '{configuredUserName}'.");
            }

            if (user.EmailConfirmed != configuredUser.EmailConfirmed)
            {
                user.EmailConfirmed = configuredUser.EmailConfirmed;
                IdentityResult updateResult = await userManager.UpdateAsync(user);
                EnsureSucceeded(updateResult, "update", email);
                LogUpdatedBootstrapUser(logger, email);
            }
            else
            {
                LogBootstrapUserAlreadyExists(logger, email);
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation, string email)
    {
        if (!result.Succeeded)
        {
            throw CreateIdentityException(result, operation, email);
        }
    }

    private static InvalidOperationException CreateIdentityException(IdentityResult result, string operation, string email)
    {
        string errors = string.Join(
            "; ",
            result.Errors.Select(static error => $"{error.Code}: {error.Description}"));
        return new InvalidOperationException($"Could not {operation} bootstrap user '{email}': {errors}");
    }

    [LoggerMessage(1, LogLevel.Information, "Created bootstrap user {Email}.")]
    private static partial void LogCreatedBootstrapUser(ILogger logger, string email);

    [LoggerMessage(2, LogLevel.Information, "Updated bootstrap user {Email}.")]
    private static partial void LogUpdatedBootstrapUser(ILogger logger, string email);

    [LoggerMessage(3, LogLevel.Debug, "Bootstrap user {Email} already exists.")]
    private static partial void LogBootstrapUserAlreadyExists(ILogger logger, string email);
}
