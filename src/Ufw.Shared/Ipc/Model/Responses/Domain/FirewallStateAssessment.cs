namespace Ufw.Shared.Ipc.Model.Responses.Domain;

/// <summary>
/// Describes authoritative firewall-state ambiguities. Occurrences use zero-based snapshot indices.
/// </summary>
public sealed record FirewallStateAssessment(IReadOnlyList<FirewallStateIssue> Issues)
{
    public static FirewallStateAssessment Clean { get; } = new([]);

    public bool IsClean => Issues.Count == 0;
}

public sealed record FirewallStateIssue(string Code, string RuleId, IReadOnlyList<int> OccurrenceIds);
