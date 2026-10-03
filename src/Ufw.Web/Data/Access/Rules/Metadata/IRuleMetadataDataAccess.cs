using Ufw.Shared.Management.Rules;

namespace Ufw.Web.Data.Access.Rules.Metadata;

/// <summary>
/// Provides persistence access to application-owned metadata attached to semantic firewall rule identities.
/// </summary>
internal interface IRuleMetadataDataAccess
{
    /// <summary>
    /// Reads metadata for the requested semantic rule identities.
    /// </summary>
    Task<IReadOnlyList<RuleMetadataItem>> GetForRuleIdsAsync(IReadOnlyCollection<string> ruleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all persisted rule metadata, including metadata whose semantic rule identity is no longer live.
    /// </summary>
    Task<IReadOnlyList<RuleMetadataItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves normalized metadata values, or removes the metadata row when all values are empty.
    /// </summary>
    Task<DataMutationResult<RuleMetadataItem?>> SaveAsync(string ruleId, RuleMetadataValues values, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-keys or copies metadata after a confirmed firewall replacement according to whether the original semantic identity remains live.
    /// </summary>
    Task<RuleMetadataReplacementPersistenceOutcome> ReconcileReplacementAsync(string originalRuleId, string replacementRuleId, bool originalRuleStillLive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes metadata for one semantic rule identity when present.
    /// </summary>
    Task<bool> DeleteAsync(string ruleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes metadata for the supplied semantic rule identities and returns the number of rows removed.
    /// </summary>
    Task<int> DeleteForRuleIdsAsync(IReadOnlyCollection<string> ruleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes only selected metadata rows whose semantic rule identity is still absent from the supplied live set.
    /// </summary>
    Task<int> DeleteUnmatchedAsync(IReadOnlyCollection<Guid> metadataIds, IReadOnlyCollection<string> liveRuleIds, CancellationToken cancellationToken = default);
}
