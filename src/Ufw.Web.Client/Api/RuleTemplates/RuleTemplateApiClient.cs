using System.Net.Http.Json;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Client.Api.RuleTemplates;

internal sealed class RuleTemplateApiClient(HttpClient httpClient) : IRuleTemplateApiClient
{
    private const string TEMPLATES_PATH = "api/v1/rule-templates";
    private static readonly Uri s_templatesUri = new(TEMPLATES_PATH, UriKind.Relative);

    public async Task<RuleTemplateInventoryResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_templatesUri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleTemplateInventoryResponse, cancellationToken);
    }

    public async Task<RuleTemplateInventoryResponse> CreateAsync(CreateRuleTemplateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.CreateRuleTemplateRequest);
        using HttpResponseMessage response = await httpClient.PostAsync(s_templatesUri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleTemplateInventoryResponse, cancellationToken);
    }

    public async Task<RuleTemplateInventoryResponse> UpdateAsync(Guid templateId, UpdateRuleTemplateRequest request, CancellationToken cancellationToken = default)
    {
        Uri uri = ApiResourceUri.ForId(TEMPLATES_PATH, templateId, nameof(templateId), "Rule template");
        ArgumentNullException.ThrowIfNull(request);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateRuleTemplateRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleTemplateInventoryResponse, cancellationToken);
    }

    public async Task<RuleTemplateInventoryResponse> DeleteAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        Uri uri = ApiResourceUri.ForId(TEMPLATES_PATH, templateId, nameof(templateId), "Rule template");
        using HttpResponseMessage response = await httpClient.DeleteAsync(uri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleTemplateInventoryResponse, cancellationToken);
    }
}
