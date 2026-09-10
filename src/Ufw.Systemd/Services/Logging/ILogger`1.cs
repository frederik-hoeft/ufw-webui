namespace Ufw.Systemd.Services.Logging;

internal interface ILogger<T> : ILoggerBase where T : class;
