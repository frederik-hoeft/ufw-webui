namespace Ufw.Systemd.Firewall;

/// <summary>
/// Serializes all UFW process invocations so concurrent daemon requests cannot
/// interleave list/mutate sequences.
/// </summary>
internal interface IUfwExecutionGate
{
    Task<TResult> RunAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken);
}
