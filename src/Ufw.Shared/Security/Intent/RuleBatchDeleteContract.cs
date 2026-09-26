using Ufw.Shared.Firewall;

namespace Ufw.Shared.Security.Intent;

public static class RuleBatchDeleteContract
{
    public static void ValidatePayload(BatchDeleteRulesPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!FirewallRuleSnapshotFingerprint.IsValid(payload.BaselineFingerprint))
        {
            throw new ArgumentException("A valid firewall snapshot fingerprint is required.", nameof(payload));
        }

        ArgumentNullException.ThrowIfNull(payload.OccurrenceIds);
        if (payload.OccurrenceIds.Length == 0)
        {
            throw new ArgumentException("At least one firewall-rule occurrence must be selected for batch deletion.", nameof(payload));
        }

        HashSet<int> seen = [];
        foreach (int occurrenceId in payload.OccurrenceIds)
        {
            if (occurrenceId < 0)
            {
                throw new ArgumentException("Batch-delete occurrence IDs cannot be negative.", nameof(payload));
            }
            if (!seen.Add(occurrenceId))
            {
                throw new ArgumentException("Batch-delete occurrence IDs must be unique.", nameof(payload));
            }
        }
    }
}
