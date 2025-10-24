namespace Ufw.Systemd.Configuration.Providers;

internal sealed class FileSystemResourceProviderStrategy : IResourceProviderStrategy
{
    public int Priority => 0;

    public Stream? OpenRead(string resourceName)
    {
        if (File.Exists(resourceName))
        {
            return File.OpenRead(resourceName);
        }
        return null;
    }
}
