namespace Ufw.Systemd.Services.Logging;

/// <summary>
/// Provides daemon logging and creates typed loggers whose scope is the consuming component type.
/// </summary>
internal interface ILogger : ILoggerBase
{
    /// <summary>Creates a logger scoped to <typeparamref name="T"/>.</summary>
    ILogger<T> Scoped<T>() where T : class;
}
