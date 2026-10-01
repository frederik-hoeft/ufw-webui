using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleTemplateService(IRuleTemplateRepository repository, IRuleMetadataValuesNormalizer metadataNormalizer) : IRuleTemplateService
{
    public Task<RuleTemplateInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public Task<RuleTemplateMutationResult> CreateAsync(CreateRuleTemplateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TryNormalize(request, out RuleTemplateValues? values)
            ? repository.CreateAsync(values, cancellationToken)
            : Task.FromResult(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.InvalidTemplate));
    }

    public Task<RuleTemplateMutationResult> UpdateAsync(Guid publicId, UpdateRuleTemplateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TryNormalize(request, out RuleTemplateValues? values)
            ? repository.UpdateAsync(publicId, values, cancellationToken)
            : Task.FromResult(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.InvalidTemplate));
    }

    public Task<RuleTemplateMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(publicId, cancellationToken);

    private bool TryNormalize(RuleTemplateRequest request, [NotNullWhen(true)] out RuleTemplateValues? values)
    {
        string name = request.Name.Trim();
        string? description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (name.Length is 0 or > RuleTemplateEntry.MAX_NAME_LENGTH
            || description?.Length > RuleTemplateEntry.MAX_DESCRIPTION_LENGTH
            || request.Rule is null
            || !metadataNormalizer.TryNormalize(request.Notes, request.TagIds, request.GroupId, out RuleMetadataValues? metadata))
        {
            values = null;
            return false;
        }

        FirewallRuleSpecification rule = RuleSpecificationNormalizer.Normalize(request.Rule);
        if (RuleSpecificationValidator.Validate(rule).Length != 0)
        {
            values = null;
            return false;
        }

        values = new RuleTemplateValues(name, description, rule, metadata);
        return true;
    }
}
