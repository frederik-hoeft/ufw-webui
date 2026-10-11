namespace Ufw.Web.Client.Services.Errors;

public interface IClientErrorMapper
{
    bool CanDescribe(Exception exception);

    ClientError Describe(Exception exception);
}
