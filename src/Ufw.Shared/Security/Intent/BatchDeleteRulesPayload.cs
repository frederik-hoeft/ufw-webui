namespace Ufw.Shared.Security.Intent;

public sealed class BatchDeleteRulesPayload
{
    public required string BaselineFingerprint { get; set; }

    public required int[] OccurrenceIds { get; set; }
}
