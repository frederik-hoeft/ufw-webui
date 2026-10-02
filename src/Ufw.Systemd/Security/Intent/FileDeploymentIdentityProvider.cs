using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Persistence;

namespace Ufw.Systemd.Security.Intent;

internal sealed class FileDeploymentIdentityProvider(IConfiguration configuration, IDurableFileStore durableFiles) : IDeploymentIdentityProvider
{
    private const int DEPLOYMENT_ID_SIZE_BYTES = 32;
    private readonly Lock _sync = new();
    private string? _deploymentId;

    public string GetDeploymentId()
    {
        string? deploymentId = Volatile.Read(ref _deploymentId);
        if (deploymentId is not null)
        {
            return deploymentId;
        }

        lock (_sync)
        {
            deploymentId = _deploymentId;
            if (deploymentId is not null)
            {
                return deploymentId;
            }

            deploymentId = LoadOrCreate();
            Volatile.Write(ref _deploymentId, deploymentId);
            return deploymentId;
        }
    }

    private string LoadOrCreate()
    {
        string? path = configuration.Settings.Security?.DeploymentIdPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Daemon deployment identity path is not configured.");
        }

        if (File.Exists(path))
        {
            return Read(path);
        }

        string generated = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(DEPLOYMENT_ID_SIZE_BYTES));
        byte[] contents = Encoding.ASCII.GetBytes(generated + "\n");
        return durableFiles.TryCreateNew(path, contents) ? generated : Read(path);
    }

    private static string Read(string path)
    {
        string value = File.ReadAllText(path).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("Daemon deployment identity file is empty.");
        }

        byte[] decoded;
        try
        {
            decoded = Base64Url.DecodeFromChars(value);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Daemon deployment identity is not valid base64url.", exception);
        }

        if (decoded.Length != DEPLOYMENT_ID_SIZE_BYTES)
        {
            throw new InvalidDataException("Daemon deployment identity has an invalid length.");
        }

        return value;
    }
}
