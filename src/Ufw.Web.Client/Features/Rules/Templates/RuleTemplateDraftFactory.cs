using Ufw.Web.Client.Features.Rules.Authoring;

namespace Ufw.Web.Client.Features.Rules.Templates;

internal sealed class RuleTemplateDraftFactory(IRuleDraftFactory ruleDraftFactory) : IRuleTemplateDraftFactory
{
    public RuleTemplateDraft Create() => new()
    {
        Rule = ruleDraftFactory.Create(),
    };

    public RuleTemplateDraft CreateFromExisting(RuleTemplate ruleTemplate)
    {
        ArgumentNullException.ThrowIfNull(ruleTemplate);
        return new RuleTemplateDraft
        {
            Name = ruleTemplate.Name,
            Description = ruleTemplate.Description,
            Rule = ruleDraftFactory.CreateFromExisting(ruleTemplate.Rule),
            Notes = ruleTemplate.Notes,
            TagIds = ruleTemplate.TagIds.ToArray(),
            GroupId = ruleTemplate.GroupId,
        };
    }
}
