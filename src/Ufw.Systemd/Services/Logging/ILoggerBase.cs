namespace Ufw.Systemd.Services.Logging;

/// <summary>
/// Provides level-specific logging operations shared by global and typed daemon loggers.
/// </summary>
internal interface ILoggerBase
{
    /// <summary>Logs diagnostic information intended for debug-mode troubleshooting.</summary>
    void LogDebug(string message);

    /// <summary>Logs an informational message describing normal daemon activity.</summary>
    void LogInformation(string message);

    /// <summary>Logs a warning that does not prevent continued operation.</summary>
    void LogWarning(string message);

    /// <summary>Logs a warning together with the exception that caused it.</summary>
    void LogWarning(Exception exception, string message);

    /// <summary>Logs an error describing an operation that could not be completed.</summary>
    void LogError(string message);

    /// <summary>Logs an error together with the exception that caused it.</summary>
    void LogError(Exception exception, string message);
}
