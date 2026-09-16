namespace Ufw.Client.Rules.Metadata;

public interface IRuleTagCatalogService
{
    IReadOnlyList<RuleTag> Current { get; }

    long Version { get; }

    Task<IReadOnlyList<RuleTag>> RefreshAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleTag>> CreateAsync(string name, string color, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleTag>> UpdateAsync(Guid tagId, string name, string color, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleTag>> DeleteAsync(Guid tagId, CancellationToken cancellationToken = default);
}
