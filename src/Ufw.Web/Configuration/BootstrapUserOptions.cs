namespace Ufw.Web.Configuration;

internal sealed class BootstrapUserOptions
{
    public string Email { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public bool EmailConfirmed { get; set; } = true;
}
