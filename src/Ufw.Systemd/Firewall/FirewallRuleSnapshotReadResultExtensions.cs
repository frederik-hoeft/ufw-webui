using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Systemd.Firewall;

internal static class FirewallRuleSnapshotReadResultExtensions
{
    public static TResult Match<TResult>(this FirewallRuleSnapshotReadResult result, Func<FirewallSnapshot, TResult> onSuccess, Func<string, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result switch
        {
            FirewallRuleSnapshotReadResult.Success success => onSuccess(success.Snapshot),
            FirewallRuleSnapshotReadResult.Failure failure => onFailure(failure.Diagnostic),
            _ => throw new InvalidOperationException($"Unsupported firewall snapshot read result '{result.GetType().Name}'."),
        };
    }

    public static FirewallSnapshot? OrDefault(this FirewallRuleSnapshotReadResult result) =>
        result.Match<FirewallSnapshot?>(static snapshot => snapshot, static _ => null);

    public static async Task<FirewallSnapshot?> OrDefaultAsync(this Task<FirewallRuleSnapshotReadResult> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        FirewallRuleSnapshotReadResult result = await resultTask;
        return result.OrDefault();
    }

    public static IResponsePayload ToResponsePayload(this FirewallRuleSnapshotReadResult result) =>
        result.Match<IResponsePayload>(static snapshot => snapshot.ToResponse(), static message => new InternalServerErrorResponse(message));

    public static bool TryGetSnapshot(this FirewallRuleSnapshotReadResult result, [NotNullWhen(true)] out FirewallSnapshot? snapshot, [NotNullWhen(false)] out IResponsePayload? error)
    {
        FirewallRuleMatchResult snapshotResult = result.Match(
            onSuccess: static snapshot => new FirewallRuleMatchResult(HasSnapshot: true, snapshot, Error: null),
            onFailure: static diagnostic => new FirewallRuleMatchResult(HasSnapshot: false, Snapshot: null, new InternalServerErrorResponse(diagnostic)));
        snapshot = snapshotResult.Snapshot;
        error = snapshotResult.Error;
        return snapshotResult.HasSnapshot;
    }
}

file readonly record struct FirewallRuleMatchResult(bool HasSnapshot, FirewallSnapshot? Snapshot, IResponsePayload? Error);
