using Ufw.Shared.Management.Rules;

namespace Ufw.Web.Data.Access.Rules.Groups;

/// <summary>
/// Provides persistence access for the rule-group catalog.
/// </summary>
public interface IRuleGroupDataAccess
{
    /// <summary>
    /// Reads the complete rule-group catalog and its current semantic memberships.
    /// </summary>
    Task<IReadOnlyList<RuleGroupItem>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a rule group from normalized, request-validated values.
    /// </summary>
    Task<DataMutationResult> CreateAsync(string name, string? comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a rule group from normalized, request-validated values.
    /// </summary>
    Task<DataMutationResult> UpdateAsync(Guid publicId, string name, string? comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an unused rule group.
    /// </summary>
    Task<DataMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
