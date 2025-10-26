namespace Ufw.Systemd.Configuration.Model;

internal sealed class NetworkOptions : IRequireValidation
{
    public int MaxConnections { get; set; } = 8;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool AssertIsValid() => this is 
    {
        MaxConnections: > 0,
    } ? true : throw new InvalidOperationException("invalid configuration");
}