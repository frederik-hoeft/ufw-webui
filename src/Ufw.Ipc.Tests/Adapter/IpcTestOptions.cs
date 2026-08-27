namespace Ufw.Ipc.Tests.Adapter;

/// <summary>
/// Tunables for a single in-process IPC test host.
/// </summary>
public sealed class IpcTestOptions
{
    /// <summary>
    /// Number of concurrent server accept loops. Defaults to 2 so a single hung connection cannot stall the host.
    /// </summary>
    public int WorkerCount { get; set; } = 2;

    /// <summary>
    /// Per-connection read/write timeout observed by the daemon-side worker.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// When true, endpoint exception messages are propagated in <c>500</c> responses (matches daemon debug mode).
    /// </summary>
    public bool DebugMode { get; set; } = true;

    /// <summary>
    /// Optional hard ceiling for a single <c>RunAsync</c> invocation, including arrange/act.
    /// </summary>
    public TimeSpan? TestTimeout { get; set; } = TimeSpan.FromMinutes(1);
}
