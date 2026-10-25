using Ufw.Shared.Ipc.Model;

namespace Ufw.Systemd.Firewall.SystemState;

internal interface IFilterSystemStateQueryService
{
    ValueTask<IResponsePayload> GetAsync(CancellationToken cancellationToken);
}
