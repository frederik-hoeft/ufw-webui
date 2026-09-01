using Ufw.Systemd.Services.Logging;

namespace Ufw.Ipc.Tests.Adapter.Configuration;

/// <summary>
/// Silent logger used by default so parallel test runs do not contend on console IO.
/// </summary>
internal class NullLogger : ILogger
{
    public static NullLogger Instance { get; } = new();

    public ILogger<T> Scoped<T>() where T : class => ScopedNullLogger<T>.Instance;

    public ILogger<T> Scoped<T>(T owner) where T : class => ScopedNullLogger<T>.Instance;

    public void LogInformation(string message)
    {
    }

    public void LogWarning(string message)
    {
    }

    public void LogWarning(Exception exception, string message)
    {
    }

    public void LogError(string message)
    {
    }

    public void LogError(Exception exception, string message)
    {
    }

    private sealed class ScopedNullLogger<T> : NullLogger, ILogger<T> where T : class
    {
        public static new ScopedNullLogger<T> Instance { get; } = new();
    }
}
