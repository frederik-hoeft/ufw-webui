namespace Ufw.Systemd.Configuration.Model;

internal interface IRequireValidation
{
    bool AssertIsValid();
}
