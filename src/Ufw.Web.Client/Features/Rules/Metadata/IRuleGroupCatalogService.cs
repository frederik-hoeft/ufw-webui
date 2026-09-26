namespace Ufw.Web.Client.Features.Rules.Metadata;

public interface IRuleGroupCatalogService
{
    IReadOnlyList<RuleGroup> Current { get; }

    long Version { get; }

    Task<IReadOnlyList<RuleGroup>> RefreshAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleGroup>> CreateAsync(string name, string? comment = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleGroup>> UpdateAsync(Guid groupId, string name, string? comment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleGroup>> DeleteAsync(Guid groupId, CancellationToken cancellationToken = default);
}
