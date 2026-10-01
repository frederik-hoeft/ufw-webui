namespace Ufw.Systemd.Configuration.Model;

internal interface IRequireValidation
{
    void ThrowIfInvalid();
}
