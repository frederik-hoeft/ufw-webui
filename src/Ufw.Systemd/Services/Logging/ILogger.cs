namespace Ufw.Systemd.Services.Logging;

internal interface ILogger : ILoggerBase
{
    ILogger<T> Scoped<T>() where T : class;

    ILogger<T> Scoped<T>(T owner) where T : class;
}

internal interface ILogger<T> : ILoggerBase where T : class;