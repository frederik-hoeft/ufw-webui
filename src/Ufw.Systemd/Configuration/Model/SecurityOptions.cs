namespace Ufw.Systemd.Configuration.Model;

internal sealed class SecurityOptions : IRequireValidation
{
    public string AuthorizedKeysPath { get; set; } = "/etc/ufw-manager/authorized_keys";

    public string NonceStorePath { get; set; } = "/var/lib/ufw-manager/intent-nonces";

    public string DeploymentIdPath { get; set; } = "/var/lib/ufw-manager/deployment-id";

    public TimeSpan MaxIntentAge { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    public bool AssertIsValid()
    {
        if (string.IsNullOrWhiteSpace(AuthorizedKeysPath)
            || string.IsNullOrWhiteSpace(NonceStorePath)
            || string.IsNullOrWhiteSpace(DeploymentIdPath)
            || MaxIntentAge <= TimeSpan.Zero
            || ClockSkew < TimeSpan.Zero)
        {
            throw new InvalidOperationException("invalid security configuration");
        }

        return true;
    }
}
