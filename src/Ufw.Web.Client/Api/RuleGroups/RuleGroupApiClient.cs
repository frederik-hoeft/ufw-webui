using System.Net.Http.Json;
using Ufw.Shared.Web;
using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Client.Api.RuleGroups;

internal sealed class RuleGroupApiClient(HttpClient httpClient) : IRuleGroupApiClient
{
    private const string GROUPS_PATH = "api/v1/rule-groups";
    private static readonly Uri s_groupsUri = new(GROUPS_PATH, UriKind.Relative);

    public async Task<RuleGroupInventoryResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_groupsUri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleGroupInventoryResponse, cancellationToken);
    }

    public async Task<RuleGroupInventoryResponse> CreateAsync(CreateRuleGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.CreateRuleGroupRequest);
        using HttpResponseMessage response = await httpClient.PostAsync(s_groupsUri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleGroupInventoryResponse, cancellationToken);
    }

    public async Task<RuleGroupInventoryResponse> UpdateAsync(Guid groupId, UpdateRuleGroupRequest request, CancellationToken cancellationToken = default)
    {
        ValidateGroupId(groupId);
        ArgumentNullException.ThrowIfNull(request);
        Uri uri = BuildGroupUri(groupId);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateRuleGroupRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleGroupInventoryResponse, cancellationToken);
    }

    public async Task<RuleGroupInventoryResponse> DeleteAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        ValidateGroupId(groupId);
        Uri uri = BuildGroupUri(groupId);
        using HttpResponseMessage response = await httpClient.DeleteAsync(uri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleGroupInventoryResponse, cancellationToken);
    }

    private static Uri BuildGroupUri(Guid groupId) => SimpleUriBuilder.Create(GROUPS_PATH)
        .AppendPath(groupId.ToString("D"))
        .BuildUri(UriKind.Relative);

    private static void ValidateGroupId(Guid groupId)
    {
        if (groupId == Guid.Empty)
        {
            throw new ArgumentException("Rule group ID must not be empty.", nameof(groupId));
        }
    }
}
