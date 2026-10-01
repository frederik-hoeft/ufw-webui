using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Configuration;

internal sealed class ConfigurationEnvironmentValidator : IConfigurationEnvironmentValidator
{
    public void ThrowIfInvalid(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!File.Exists(settings.UfwPath))
        {
            throw new InvalidOperationException($"The configured UFW executable does not exist: '{settings.UfwPath}'.");
        }

        ThrowIfTransportEnvironmentInvalid(settings.Transport);
        ThrowIfSecurityEnvironmentInvalid(settings.Security);
    }

    private static void ThrowIfTransportEnvironmentInvalid(TransportOptions transport)
    {
        if (transport.Type is TransportType.Pipe)
        {
            ThrowIfPipeEnvironmentInvalid(transport.Pipe!);
        }

        ThrowIfTransportSecurityEnvironmentInvalid(transport.Security);
    }

    private static void ThrowIfPipeEnvironmentInvalid(PipeOptions pipe)
    {
        if (!OperatingSystem.IsWindows() && (!Path.IsPathFullyQualified(pipe.PipeName) || Path.EndsInDirectorySeparator(pipe.PipeName)))
        {
            throw new InvalidOperationException("Unix pipe endpoints must be absolute file paths.");
        }
    }

    private static void ThrowIfTransportSecurityEnvironmentInvalid(TransportSecurityOptions security)
    {
        if (!security.TlsEnabled)
        {
            return;
        }

        if (!File.Exists(security.ServerCertificatePath) || !File.Exists(security.ServerCertificateKeyPath))
        {
            throw new InvalidOperationException("TLS requires readable server certificate and private-key files.");
        }
    }

    private static void ThrowIfSecurityEnvironmentInvalid(SecurityOptions? security)
    {
        if (security is null)
        {
            return;
        }

        if (Directory.Exists(security.AuthorizedKeysPath)
            || Directory.Exists(security.NonceStorePath)
            || Directory.Exists(security.DeploymentIdPath)
            || Directory.Exists(security.ReorderRecoveryJournalPath))
        {
            throw new InvalidOperationException("Security file paths must not refer to directories.");
        }
    }
}
