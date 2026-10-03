using Ufw.Ipc.Client;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Api.V1.Errors;

public interface IDaemonApiErrorMapper
{
    DaemonApiError MapProxyFailure(UfwIpcException exception);

    DaemonApiError MapUnavailable(UfwIpcException exception);

    DaemonApiError MapInvalidResponse(DaemonInvalidResponseException exception);
}
