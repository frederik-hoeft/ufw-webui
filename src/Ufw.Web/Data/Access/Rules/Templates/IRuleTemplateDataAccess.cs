using Ufw.Shared.Management.Rules;

namespace Ufw.Web.Data.Access.Rules.Templates;

/// <summary>
/// Provides persistence access for reusable rule templates.
/// </summary>
public interface IRuleTemplateDataAccess
{
    /// <summary>
    /// Reads the complete rule-template catalog.
    /// </summary>
    Task<IReadOnlyList<RuleTemplateItem>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a rule template from normalized, semantically validated values.
    /// </summary>
    Task<DataMutationResult> CreateAsync(RuleTemplateValues values, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a rule template from normalized, semantically validated values.
    /// </summary>
    Task<DataMutationResult> UpdateAsync(Guid publicId, RuleTemplateValues values, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a rule template.
    /// </summary>
    Task<DataMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
