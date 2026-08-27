namespace Ufw.Ipc.Shared.Threading;

public interface IAccess<out T> : IDisposable where T : class
{
    T Value { get; }
}
