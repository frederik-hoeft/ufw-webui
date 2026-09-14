using Ufw.Shared.Firewall;
using Ufw.Systemd.Services.Logging;
using DaemonConfiguration = Ufw.Systemd.Configuration.IConfiguration;

namespace Ufw.Systemd.Interop.Configuration;

internal sealed class UfwDefaultsReader(DaemonConfiguration configuration, ILogger logger) : IUfwDefaultsReader
{
    private readonly ILogger<UfwDefaultsReader> _logger = logger.Scoped<UfwDefaultsReader>();

    public async ValueTask<FirewallConfigurationSnapshot?> ReadAsync(CancellationToken cancellationToken)
    {
        string path = configuration.Settings.UfwDefaultsPath;
        try
        {
            string contents = await File.ReadAllTextAsync(path, cancellationToken);
            if (UfwDefaultsParser.TryParse(contents, out FirewallConfigurationSnapshot? snapshot))
            {
                return snapshot;
            }

            _logger.LogError($"UFW defaults file '{path}' is missing required IPv6 or default-policy settings, or contains unsupported values.");
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _logger.LogError(exception, $"Failed to read UFW defaults file '{path}'.");
            return null;
        }
    }
}
