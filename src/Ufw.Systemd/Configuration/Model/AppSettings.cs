using System.Text.Json.Serialization;

namespace Ufw.Systemd.Configuration.Model;

internal sealed class AppSettings : IRequireValidation
{
    public required bool DebugMode { get; init; }

    public required bool ExposeRemoteExceptionDetails { get; init; }

    public required string UfwPath { get; init; }

    public required string UfwDefaultsPath { get; init; }

    [JsonPropertyName("iptables_save_path")]
    public required string IptablesSavePath { get; init; }

    [JsonPropertyName("ip6tables_save_path")]
    public required string Ip6TablesSavePath { get; init; }

    [JsonPropertyName("ufw_user_rules_path")]
    public required string UfwUserRulesPath { get; init; }

    [JsonPropertyName("ufw_user6_rules_path")]
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
            || string.IsNullOrWhiteSpace(Ip6TablesSavePath)
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
