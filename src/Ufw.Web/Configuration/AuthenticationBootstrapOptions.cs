namespace Ufw.Web.Configuration;

internal sealed class AuthenticationBootstrapOptions
{
    public const string SECTION_NAME = "Auth:Bootstrap";

    public List<BootstrapUserOptions> Users { get; } = [];

    public bool IsValid()
    {
        HashSet<string> emails = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> userNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (BootstrapUserOptions user in Users)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }

            string email = user.Email.Trim();
            string userName = string.IsNullOrWhiteSpace(user.UserName) ? email : user.UserName.Trim();
            if (!emails.Add(email) || !userNames.Add(userName))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class BootstrapUserOptions
{
    public string Email { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public bool EmailConfirmed { get; set; } = true;
}
