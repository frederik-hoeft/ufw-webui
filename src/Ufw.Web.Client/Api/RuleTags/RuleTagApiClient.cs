using System.Net.Http.Json;
using Ufw.Web.Client.Api;
using Ufw.Web.Model.V1.RuleTags;

namespace Ufw.Web.Client.Api.RuleTags;

internal sealed class RuleTagApiClient(HttpClient httpClient) : IRuleTagApiClient
{
    private const string TAGS_PATH = "api/v1/rule-tags";
    private static readonly Uri s_tagsUri = new(TAGS_PATH, UriKind.Relative);

    public async Task<RuleTagInventoryResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_tagsUri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleTagInventoryResponse, cancellationToken);
    }

    public async Task<RuleTagInventoryResponse> CreateAsync(CreateRuleTagRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.CreateRuleTagRequest);
        using HttpResponseMessage response = await httpClient.PostAsync(s_tagsUri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleTagInventoryResponse, cancellationToken);
    }

    public async Task<RuleTagInventoryResponse> UpdateAsync(Guid tagId, UpdateRuleTagRequest request, CancellationToken cancellationToken = default)
    {
        Uri uri = ApiResourceUri.ForId(TAGS_PATH, tagId, nameof(tagId), "Rule tag");
        ArgumentNullException.ThrowIfNull(request);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateRuleTagRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleTagInventoryResponse, cancellationToken);
    }

    public async Task<RuleTagInventoryResponse> DeleteAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        Uri uri = ApiResourceUri.ForId(TAGS_PATH, tagId, nameof(tagId), "Rule tag");
        using HttpResponseMessage response = await httpClient.DeleteAsync(uri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleTagInventoryResponse, cancellationToken);
    }
}
