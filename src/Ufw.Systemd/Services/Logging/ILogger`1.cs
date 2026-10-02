namespace Ufw.Systemd.Services.Logging;

/// <summary>
/// Provides logging scoped to component type <typeparamref name="T"/>.
/// </summary>
internal interface ILogger<T> : ILoggerBase where T : class;
