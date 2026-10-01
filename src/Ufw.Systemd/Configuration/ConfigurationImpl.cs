using System.Text.Json;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Configuration;

internal sealed class ConfigurationImpl(
    AppSettingsJsonSerializerContext jsonSerializerContext,
    IConfigurationEnvironmentValidator environmentValidator) : IConfiguration
{
    private AppSettings? _settings;
    private int _loadStarted;

    public AppSettings Settings => Volatile.Read(ref _settings) ?? throw new InvalidOperationException("Configuration has not been loaded.");

    public async ValueTask LoadAsync(string settingsPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        if (Interlocked.CompareExchange(ref _loadStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException("Configuration can only be loaded once during process startup.");
        }

        await using FileStream stream = File.OpenRead(settingsPath);
        AppSettings settings = await JsonSerializer.DeserializeAsync(
            stream,
            jsonSerializerContext.GetTypeInfo<AppSettings>(),
            cancellationToken) ?? throw new JsonException("The configuration root must be a JSON object.");

        settings.ThrowIfInvalid();
        environmentValidator.ThrowIfInvalid(settings);
        Volatile.Write(ref _settings, settings);
    }
}
