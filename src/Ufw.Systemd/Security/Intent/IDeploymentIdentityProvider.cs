namespace Ufw.Systemd.Security.Intent;

internal interface IDeploymentIdentityProvider
{
    string GetDeploymentId();
}
