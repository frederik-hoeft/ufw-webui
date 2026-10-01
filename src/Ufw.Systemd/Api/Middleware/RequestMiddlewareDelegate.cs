using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Middleware;

internal delegate ValueTask<IResponseMessage> RequestMiddlewareDelegate(IRequestMessage request, CancellationToken cancellationToken);
