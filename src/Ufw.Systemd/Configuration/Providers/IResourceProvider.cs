namespace Ufw.Systemd.Configuration.Providers;

internal interface IResourceProvider
{
    IResourceProviderStrategy? PreferredStrategy { get; set; }

    Stream? OpenRead(string resourceName);
}
