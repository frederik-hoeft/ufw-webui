using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleGroupService(IRuleGroupRepository repository) : IRuleGroupService
{
    public Task<RuleGroupInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public Task<RuleGroupMutationResult> CreateAsync(CreateRuleGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryNormalize(request.Name, request.Comment, out RuleGroupValues values))
        {
            return Task.FromResult(new RuleGroupMutationResult(RuleGroupMutationOutcome.InvalidGroup));
        }
        return repository.CreateAsync(values.Name, values.Comment, cancellationToken);
    }

    public Task<RuleGroupMutationResult> UpdateAsync(Guid publicId, UpdateRuleGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryNormalize(request.Name, request.Comment, out RuleGroupValues values))
        {
            return Task.FromResult(new RuleGroupMutationResult(RuleGroupMutationOutcome.InvalidGroup));
        }
        return repository.UpdateAsync(publicId, values.Name, values.Comment, cancellationToken);
    }

    public Task<RuleGroupMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(publicId, cancellationToken);

    private static bool TryNormalize(string name, string? comment, out RuleGroupValues values)
    {
        string normalizedName = name.Trim();
        string? normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (normalizedName.Length is 0 or > RuleGroupEntry.MAX_NAME_LENGTH || normalizedComment?.Length > RuleGroupEntry.MAX_COMMENT_LENGTH)
        {
            values = default;
            return false;
        }

        values = new RuleGroupValues(normalizedName, normalizedComment);
        return true;
    }

    private readonly record struct RuleGroupValues(string Name, string? Comment);
}
