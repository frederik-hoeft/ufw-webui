using Ufw.Shared.Management.Rules;

namespace Ufw.Web.Model.V1.RuleTemplates;

public sealed class RuleTemplateInventoryResponse
{
    public RuleTemplateInventoryResponse() { }

    public RuleTemplateInventoryResponse(IReadOnlyList<RuleTemplateItem> templates) => Templates = templates;

    public IReadOnlyList<RuleTemplateItem> Templates { get; init; } = [];
}
