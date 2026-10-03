using Ufw.Ipc.Client;

namespace Ufw.Web.Services.Daemon;

/// <summary>
/// Represents a daemon failure that the consuming workflow classifies as upstream unavailability.
/// </summary>
public sealed class DaemonUnavailableException(UfwIpcException error)
    : InvalidOperationException(error.ResponseMessage ?? "The daemon is unavailable.", error)
{
    public UfwIpcException Error { get; } = error;
}
