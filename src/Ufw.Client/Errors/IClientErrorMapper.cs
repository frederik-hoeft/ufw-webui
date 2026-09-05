namespace Ufw.Client.Errors;

public interface IClientErrorMapper
{
    bool TryDescribe(Exception exception, out ClientError error);

    ClientError Describe(Exception exception);
}
