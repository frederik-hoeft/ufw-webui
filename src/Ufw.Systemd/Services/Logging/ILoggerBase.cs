namespace Ufw.Systemd.Services.Logging;

internal interface ILoggerBase
{
    void LogInformation(string message);
    void LogWarning(string message);
    void LogWarning(Exception exception, string message);
    void LogError(string message);
    void LogError(Exception exception, string message);
}
