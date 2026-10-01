using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall;

internal static class FirewallRuleSnapshotReadResultExtensions
{
    public static TResult Match<TResult>(
        this FirewallRuleSnapshotReadResult result,
        Func<RuleListResponse, TResult> onSuccess,
        Func<IResponsePayload, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result switch
        {
            FirewallRuleSnapshotReadResult.Success success => onSuccess(success.Snapshot),
            FirewallRuleSnapshotReadResult.Failure failure => onFailure(failure.Error),
            _ => throw new InvalidOperationException($"Unsupported firewall snapshot read result '{result.GetType().Name}'."),
        };
    }

    public static RuleListResponse? OrDefault(this FirewallRuleSnapshotReadResult result) =>
        result.Match<RuleListResponse?>(static snapshot => snapshot, static _ => null);

    public static async Task<RuleListResponse?> OrDefaultAsync(this Task<FirewallRuleSnapshotReadResult> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        return (await resultTask).OrDefault();
    }

    public static IResponsePayload ToResponsePayload(this FirewallRuleSnapshotReadResult result) =>
        result.Match<IResponsePayload>(static snapshot => snapshot, static error => error);

    public static bool TryGetSnapshot(
        this FirewallRuleSnapshotReadResult result,
        [NotNullWhen(true)] out RuleListResponse? snapshot,
        [NotNullWhen(false)] out IResponsePayload? error)
    {
        (bool HasSnapshot, RuleListResponse? Snapshot, IResponsePayload? Error) projection = result.Match<(bool, RuleListResponse?, IResponsePayload?)>(
            static snapshot => (true, snapshot, null),
            static error => (false, null, error));
        snapshot = projection.Snapshot;
        error = projection.Error;
        return projection.HasSnapshot;
    }
}
