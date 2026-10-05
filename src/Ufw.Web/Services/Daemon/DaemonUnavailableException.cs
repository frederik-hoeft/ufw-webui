using Ufw.Ipc.Client;

namespace Ufw.Web.Services.Daemon;

/// <summary>
/// Represents a daemon failure that the consuming workflow classifies as upstream unavailability.
/// </summary>
internal sealed class DaemonUnavailableException(UfwIpcError error)
    : InvalidOperationException(error.ResponseMessage ?? "The daemon is unavailable.")
{
    public UfwIpcError Error { get; } = error;
}
