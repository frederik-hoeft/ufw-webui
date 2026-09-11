using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Middleware;

internal interface IRequestResponsePipeline
{
    ValueTask<IResponseMessage> ProcessMessageAsync(IRequestMessage request, CancellationToken cancellationToken);
}
