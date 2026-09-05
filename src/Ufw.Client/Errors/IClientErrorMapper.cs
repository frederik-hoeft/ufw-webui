namespace Ufw.Client.Errors;

public interface IClientErrorMapper
{
    bool TryDescribe(Exception exception, out ClientError clientError);

    ClientError Describe(Exception exception);
}
