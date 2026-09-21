using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Model.V1.Rules;

public sealed class RuleInventoryResponse
{
    public RuleInventoryResponse() { }

    public RuleInventoryResponse(RuleListResponse firewall, IReadOnlyList<RuleMetadataItem> metadata) =>
        (Firewall, Metadata) = (firewall, metadata);

    public RuleListResponse Firewall { get; init; } = null!;

    public IReadOnlyList<RuleMetadataItem> Metadata { get; init; } = [];
}
