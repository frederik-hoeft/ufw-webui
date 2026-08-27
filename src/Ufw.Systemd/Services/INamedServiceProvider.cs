namespace Ufw.Systemd.Services;

internal interface INamedServiceProvider : IServiceProvider
{
    T? GetService<T>(string name) where T : class;
}
