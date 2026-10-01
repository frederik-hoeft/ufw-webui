namespace Ufw.Systemd.Configuration.Model;

internal sealed class TransportOptions : IRequireValidation
{
    public required TransportType Type { get; init; }

    public required PipeOptions? Pipe { get; init; }

    public required TcpOptions? Tcp { get; init; }

    public required TransportSecurityOptions Security { get; init; }

    public void ThrowIfInvalid()
    {
        if (!Enum.IsDefined(Type))
        {
            throw new InvalidOperationException($"Unsupported transport type '{Type}'.");
        }

        if (Security is null)
        {
            throw new InvalidOperationException("Transport security settings are required.");
        }

        Pipe?.ThrowIfInvalid();
        Tcp?.ThrowIfInvalid();
        Security.ThrowIfInvalid();

        if (Type is TransportType.Pipe && Pipe is null)
        {
            throw new InvalidOperationException("Pipe transport settings are required when the pipe transport is selected.");
        }

        if (Type is TransportType.Tcp && Tcp is null)
        {
            throw new InvalidOperationException("TCP transport settings are required when the TCP transport is selected.");
        }
    }
}
