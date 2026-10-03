using Ufw.Ipc.Client;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Api.V1.Errors;

public interface IDaemonApiErrorMapper
{
    DaemonApiError MapProxyFailure(UfwIpcError daemonError);

    DaemonApiError MapUnavailable(UfwIpcError daemonError);

    DaemonApiError MapInvalidResponse(DaemonInvalidResponseException exception);
}
