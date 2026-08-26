namespace Ufw.Web.Services.Auth;

public interface IAuthenticationTimingService
{
    void PerformDummyPasswordVerification(string suppliedPassword);
}
