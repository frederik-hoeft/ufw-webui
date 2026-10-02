namespace Ufw.Systemd.Configuration.Model;

internal sealed class NetworkOptions : IRequireValidation
{
    public required int MaxConnections { get; init; }

    public required TimeSpan IoTimeout { get; init; }

    public required TimeSpan RequestTimeout { get; init; }

    public void ThrowIfInvalid()
    {
        if (MaxConnections <= 0 || !IsValidTimeout(IoTimeout) || !IsValidTimeout(RequestTimeout))
        {
            throw new InvalidOperationException("Network connection limits and timeouts must be positive, or use an infinite timeout.");
        }
    }

    private static bool IsValidTimeout(TimeSpan timeout) =>
        timeout == Timeout.InfiniteTimeSpan || timeout > TimeSpan.Zero;
}
