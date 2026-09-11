using Ufw.Ipc.Client;

namespace Ufw.Web.Api.V1.Errors;

public interface IDaemonApiErrorMapper
{
    DaemonApiError MapProxyFailure(UfwIpcException exception);

    DaemonApiError MapUnavailable(UfwIpcException exception);

    DaemonApiError MapInvalidResponse(InvalidDataException exception);
}
