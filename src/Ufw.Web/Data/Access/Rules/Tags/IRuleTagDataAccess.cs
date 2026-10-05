using Ufw.Shared.Management.Rules;

namespace Ufw.Web.Data.Access.Rules.Tags;

/// <summary>
/// Provides persistence access for the rule-tag catalog.
/// </summary>
public interface IRuleTagDataAccess
{
    /// <summary>
    /// Reads the complete rule-tag catalog.
    /// </summary>
    Task<IReadOnlyList<RuleTagItem>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a rule tag from normalized, request-validated values.
    /// </summary>
    Task<DataMutationResult> CreateAsync(string name, string color, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a rule tag from normalized, request-validated values.
    /// </summary>
    Task<DataMutationResult> UpdateAsync(Guid publicId, string name, string color, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an unused rule tag.
    /// </summary>
    Task<DataMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
