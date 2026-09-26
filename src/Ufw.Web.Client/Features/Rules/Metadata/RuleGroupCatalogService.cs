using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.RuleGroups;
using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal sealed class RuleGroupCatalogService(IRuleGroupApiClient apiClient) : IRuleGroupCatalogService
{
    public IReadOnlyList<RuleGroup> Current { get; private set; } = [];

    public long Version { get; private set; }

    public async Task<IReadOnlyList<RuleGroup>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.GetAsync(cancellationToken));
        return Current;
    }

    public async Task<IReadOnlyList<RuleGroup>> CreateAsync(string name, string? comment = null, CancellationToken cancellationToken = default)
    {
        RuleGroupInventoryResponse response = await apiClient.CreateAsync(new CreateRuleGroupRequest
        {
            Name = name,
            Comment = comment,
        }, cancellationToken);
        Current = Normalize(response);
        Version++;
        return Current;
    }

    public async Task<IReadOnlyList<RuleGroup>> UpdateAsync(Guid groupId, string name, string? comment, CancellationToken cancellationToken = default)
    {
        RuleGroupInventoryResponse response = await apiClient.UpdateAsync(groupId, new UpdateRuleGroupRequest
        {
            Name = name,
            Comment = comment,
        }, cancellationToken);
        Current = Normalize(response);
        Version++;
        return Current;
    }

    public async Task<IReadOnlyList<RuleGroup>> DeleteAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.DeleteAsync(groupId, cancellationToken));
        Version++;
        return Current;
    }

    private static IReadOnlyList<RuleGroup> Normalize(RuleGroupInventoryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Groups is null)
        {
            throw new ApiProtocolException("Rule-group inventory response is missing the group list.");
        }

        List<RuleGroup> groups = new(response.Groups.Count);
        foreach (RuleGroupItem item in response.Groups)
        {
            if (item is null || item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name) || item.RuleIds is null)
            {
                throw new ApiProtocolException("Rule-group inventory response contains an invalid group entry.");
            }

            string[] ruleIds = [.. item.RuleIds.Select(static ruleId => ruleId?.Trim()).Where(static ruleId => !string.IsNullOrWhiteSpace(ruleId)).Cast<string>()];
            if (ruleIds.Length != item.RuleIds.Count || ruleIds.Distinct(StringComparer.Ordinal).Count() != ruleIds.Length)
            {
                throw new ApiProtocolException("Rule-group inventory response contains an invalid member identity.");
            }

            groups.Add(new RuleGroup(item.Id, item.Name.Trim(), string.IsNullOrWhiteSpace(item.Comment) ? null : item.Comment.Trim(), ruleIds));
        }

        if (groups.Select(static group => group.Id).Distinct().Count() != groups.Count
            || groups.Select(static group => group.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != groups.Count)
        {
            throw new ApiProtocolException("Rule-group inventory response contains duplicate group identities.");
        }

        return groups
            .OrderBy(static group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static group => group.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
