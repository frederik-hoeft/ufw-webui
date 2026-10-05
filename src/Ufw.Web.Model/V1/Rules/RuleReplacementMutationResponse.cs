using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Model.V1.Rules;

public sealed class RuleReplacementMutationResponse
{
    public RuleReplacementMutationResponse() { }

    public RuleReplacementMutationResponse(
        RuleReplacementResponse firewall,
        RuleReplacementMetadataReconciliationOutcome metadataReconciliation,
        string? metadataDiagnostic = null) =>
        (Firewall, MetadataReconciliation, MetadataDiagnostic) = (firewall, metadataReconciliation, metadataDiagnostic);

    public RuleReplacementResponse Firewall { get; init; } = null!;

    public RuleReplacementMetadataReconciliationOutcome MetadataReconciliation { get; init; }

    public string? MetadataDiagnostic { get; init; }
}
