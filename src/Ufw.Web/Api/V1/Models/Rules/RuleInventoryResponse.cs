using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Api.V1.Models.Rules;

public sealed record RuleInventoryResponse(RuleListResponse Firewall, IReadOnlyList<RuleMetadataItem> Metadata);
