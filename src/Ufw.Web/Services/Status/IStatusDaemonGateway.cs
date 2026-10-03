namespace Ufw.Web.Services.Status;

/// <summary>
/// Provides the daemon status probe used by the Web status endpoint while keeping IPC route details private to the gateway.
/// </summary>
public interface IStatusDaemonGateway
{
    Task GetStatusAsync(CancellationToken cancellationToken = default);
}
