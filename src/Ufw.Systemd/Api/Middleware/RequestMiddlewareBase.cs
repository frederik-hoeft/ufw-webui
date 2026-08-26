using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Systemd.Api.Middleware;

internal abstract class RequestMiddlewareBase : IRequestMiddleware
{
    public abstract int Priority { get; }

    protected IRequestMiddleware Next
    {
        get => field ?? throw new InvalidOperationException("middleware not initialized");
        private set
        {
            if (field is not null)
            {
                throw new InvalidOperationException("middleware already initialized");
            }
            field = value;
        }
    }

    public void Initialize(IRequestMiddleware next)
    {
        ArgumentNullException.ThrowIfNull(next);
        Next = next;
    }

    public abstract ValueTask<IMessage> InvokeAsync(IMessage request, CancellationToken cancellationToken);
}