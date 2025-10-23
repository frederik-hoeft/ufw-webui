using ConsoleAppFramework;

namespace Ufw.Systemd;

public class Commands
{
    [Command("serve")]
    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        await using DefaultServiceProvider serviceProvider = new();

    }
}
