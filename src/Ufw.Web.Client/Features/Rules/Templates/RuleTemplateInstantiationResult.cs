namespace Ufw.Web.Client.Features.Rules.Templates;

internal sealed record RuleTemplateInstantiationResult(
    RuleTemplateInstantiation? Instantiation,
    RuleTemplateInstantiationError Error)
{
    public bool Succeeded => Instantiation is not null && Error == RuleTemplateInstantiationError.None;

    public static RuleTemplateInstantiationResult Success(RuleTemplateInstantiation instantiation) => new(instantiation, RuleTemplateInstantiationError.None);

    public static RuleTemplateInstantiationResult Failure(RuleTemplateInstantiationError error) => new(null, error);
}
