using Ufw.Mock.Cli;
using Ufw.Mock.State;

namespace Ufw.Mock.Services;

internal sealed class UfwCompatibilityConfiguration
{
    private const string UFW_DEFAULTS_PATH = "UFW_DEFAULTS_PATH";

    public bool IsIPv6Enabled(UfwMockState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string? defaultsPath = Environment.GetEnvironmentVariable(UFW_DEFAULTS_PATH);
        if (string.IsNullOrWhiteSpace(defaultsPath))
        {
            return state.IPv6Enabled;
        }

        try
        {
            foreach (string line in File.ReadLines(defaultsPath))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                int separator = trimmed.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = trimmed[..separator].Trim();
                if (!key.Equals("IPV6", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = trimmed[(separator + 1)..].Trim().Trim('"', '\'');
                return value.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (IOException exception)
        {
            throw new UfwCliException($"Couldn't open '{defaultsPath}' for reading", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UfwCliException($"Couldn't open '{defaultsPath}' for reading", exception);
        }

        return false;
    }
}
