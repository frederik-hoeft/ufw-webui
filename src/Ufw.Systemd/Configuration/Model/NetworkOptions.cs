namespace Ufw.Systemd.Configuration.Model;

internal sealed class NetworkOptions : IRequireValidation
{
    public int MaxConnections { get; set; } = 8;

    public TimeSpan IoTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool AssertIsValid()
    {
        if (MaxConnections <= 0 || !IsValidTimeout(IoTimeout) || !IsValidTimeout(RequestTimeout))
        {
            throw new InvalidOperationException("invalid configuration");
        }

        return true;
    }

    private static bool IsValidTimeout(TimeSpan timeout) =>
        timeout == Timeout.InfiniteTimeSpan || timeout > TimeSpan.Zero;
}
