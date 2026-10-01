namespace Ufw.Web.Client.Features.Rules.Templates;

public interface IRuleTemplateDraftFactory
{
    RuleTemplateDraft Create();

    RuleTemplateDraft CreateFromExisting(RuleTemplate ruleTemplate);
}
