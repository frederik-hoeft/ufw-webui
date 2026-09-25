namespace Ufw.Web.Client.Services.Errors;

public interface IClientErrorMapper
{
    bool TryDescribe(Exception exception, out ClientError clientError);

    ClientError Describe(Exception exception);
}
