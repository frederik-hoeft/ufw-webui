using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Api.Rules.Model;

public sealed class RuleInventoryResponse
{
    public RuleListResponse Firewall { get; init; } = null!;

    public IReadOnlyList<RuleMetadataItem> Metadata { get; init; } = [];
}
