using System.Text.Json.Serialization;

namespace Ufw.Systemd.Configuration.Model;

internal sealed class AppSettings : IRequireValidation
{
    public required bool DebugMode { get; init; }

    public required bool ExposeRemoteExceptionDetails { get; init; }

    public required string UfwPath { get; init; }

    public required string UfwDefaultsPath { get; init; }

    // The AOT JSON bindings may create an object without running its initializers.
    // Resolve optional read-only observation paths in getters so existing settings files remain valid.
#pragma warning disable IDE0032 // Explicit backing fields preserve defaults with the AOT configuration binding generator.
    private string? _iptablesSavePath;
    private string? _ip6TablesSavePath;
    private string? _ufwUserRulesPath;
    private string? _ufwUser6RulesPath;

    [JsonPropertyName("iptables_save_path")]
    public string IptablesSavePath { get => _iptablesSavePath ?? "/usr/sbin/iptables-save"; init => _iptablesSavePath = value; }

    [JsonPropertyName("ip6tables_save_path")]
    public string Ip6TablesSavePath { get => _ip6TablesSavePath ?? "/usr/sbin/ip6tables-save"; init => _ip6TablesSavePath = value; }

    [JsonPropertyName("ufw_user_rules_path")]
    public string UfwUserRulesPath { get => _ufwUserRulesPath ?? "/etc/ufw/user.rules"; init => _ufwUserRulesPath = value; }

    [JsonPropertyName("ufw_user6_rules_path")]
    public string UfwUser6RulesPath { get => _ufwUser6RulesPath ?? "/etc/ufw/user6.rules"; init => _ufwUser6RulesPath = value; }

#pragma warning restore IDE0032

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
