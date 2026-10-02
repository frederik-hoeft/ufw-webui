namespace Ufw.Systemd.Services.Logging;

internal class ConsoleLogger : ILogger
{
    protected virtual string ScopeName => "Global";

    public ILogger<T> Scoped<T>() where T : class => ScopedConsoleLogger<T>.Instance;

    public void LogDebug(string message) => Console.WriteLine($"[DEBUG] [{ScopeName}] {message}");

    public void LogInformation(string message) => Console.WriteLine($"[INFO] [{ScopeName}] {message}");

    public void LogWarning(string message) => Console.WriteLine($"[WARN] [{ScopeName}] {message}");

    public void LogWarning(Exception exception, string message) => Console.WriteLine($"[WARN] [{ScopeName}] {message} Exception: {exception}");

    public void LogError(string message) => Console.WriteLine($"[ERROR] [{ScopeName}] {message}");

    public void LogError(Exception exception, string message) => Console.WriteLine($"[ERROR] [{ScopeName}] {message} Exception: {exception}");

    private sealed class ScopedConsoleLogger<T> : ConsoleLogger, ILogger<T> where T : class
    {
        protected override string ScopeName { get; } = typeof(T).Name ?? "UNKNOWN";

        public static ScopedConsoleLogger<T> Instance { get; } = new ScopedConsoleLogger<T>();
    }
}
