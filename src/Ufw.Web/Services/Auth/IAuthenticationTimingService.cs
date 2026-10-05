namespace Ufw.Web.Services.Auth;

internal interface IAuthenticationTimingService
{
    void PerformDummyPasswordVerification(string suppliedPassword);
}
