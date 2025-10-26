using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Systemd.Api.Middleware;

internal abstract class RequestMiddlewareBase : IRequestMiddleware
{
    private IRequestMiddleware? _next;

    public abstract int Priority { get; }

    protected IRequestMiddleware Next => _next ?? throw new InvalidOperationException("middleware not initialized");

    public void Initialize(IRequestMiddleware next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (_next is not null)
        {
            throw new InvalidOperationException("middleware already initialized");
        }
        _next = next;
    }

    public abstract ValueTask<IMessage> InvokeAsync(IMessage request, CancellationToken cancellationToken);
}