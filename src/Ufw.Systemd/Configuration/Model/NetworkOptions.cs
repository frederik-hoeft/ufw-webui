namespace Ufw.Systemd.Configuration.Model;

internal sealed class NetworkOptions : IRequireValidation
{
    public int MaxConcurrentConnections { get; set; } = 5;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool AssertIsValid() => this is 
    {
        MaxConcurrentConnections: > 0,
    } ? true : throw new InvalidOperationException("invalid configuration");
}