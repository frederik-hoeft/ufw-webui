using ConsoleAppFramework;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd;

public class Commands
{
    [Command("serve")]
    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        await using DefaultServiceProvider serviceProvider = new();
        IConfiguration configuration = serviceProvider.GetService<IConfiguration>();
        bool success = await configuration.TryReloadAsync("/etc/ufw-manager/settings.json", cancellationToken);
        if (!success)
        {
            await Console.Error.WriteLineAsync("Failed to load service configuration from /etc/ufw-manager/settings.json");
            throw new InvalidOperationException("failed to load service configuration");
        }
    }
}
