using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Api;

public sealed class RuleInventoryResponse
{
    public RuleListResponse Firewall { get; init; } = null!;

    public IReadOnlyList<RuleMetadataItem> Metadata { get; init; } = [];
}
