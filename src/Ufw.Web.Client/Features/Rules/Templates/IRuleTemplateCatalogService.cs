namespace Ufw.Web.Client.Features.Rules.Templates;

public interface IRuleTemplateCatalogService
{
    IReadOnlyList<RuleTemplate> Current { get; }

    Task<IReadOnlyList<RuleTemplate>> RefreshAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleTemplate>> CreateAsync(RuleTemplateDefinition definition, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleTemplate>> UpdateAsync(Guid templateId, RuleTemplateDefinition definition, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleTemplate>> DeleteAsync(Guid templateId, CancellationToken cancellationToken = default);
}
