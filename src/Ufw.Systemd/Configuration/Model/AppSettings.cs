namespace Ufw.Systemd.Configuration.Model;

internal sealed class AppSettings : IRequireValidation
{
    public required bool DebugMode { get; init; }

    public required bool ExposeRemoteExceptionDetails { get; init; }

    public required string UfwPath { get; init; }

    public required string UfwDefaultsPath { get; init; }

    public required bool FilterSystemStateEnabled { get; init; }

    public required string IptablesSavePath { get; init; }

    public required string Ip6tablesSavePath { get; init; }

    public required string UfwUserRulesPath { get; init; }

    public required string UfwUser6RulesPath { get; init; }

    public required TransportOptions Transport { get; init; }

    public required NetworkOptions Network { get; init; }

    public required SecurityOptions? Security { get; init; }

    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(UfwPath) || string.IsNullOrWhiteSpace(UfwDefaultsPath))
        {
            throw new InvalidOperationException("UFW executable and defaults paths are required.");
        }

        if (string.IsNullOrWhiteSpace(IptablesSavePath)
            || string.IsNullOrWhiteSpace(Ip6tablesSavePath)
            || string.IsNullOrWhiteSpace(UfwUserRulesPath)
            || string.IsNullOrWhiteSpace(UfwUser6RulesPath))
        {
            throw new InvalidOperationException("iptables-save and UFW user-rules paths are required.");
        }

        if (Transport is null)
        {
            throw new InvalidOperationException("Transport settings are required.");
        }

        Transport.ThrowIfInvalid();
        Network.ThrowIfInvalid();
        Security?.ThrowIfInvalid();
    }
}
