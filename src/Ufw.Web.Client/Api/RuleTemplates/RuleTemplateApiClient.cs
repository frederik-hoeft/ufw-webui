using System.Net.Http.Json;
using Ufw.Shared.Web;
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
        ValidateTemplateId(templateId);
        ArgumentNullException.ThrowIfNull(request);
        Uri uri = BuildTemplateUri(templateId);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateRuleTemplateRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleTemplateInventoryResponse, cancellationToken);
    }

    public async Task<RuleTemplateInventoryResponse> DeleteAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        ValidateTemplateId(templateId);
        Uri uri = BuildTemplateUri(templateId);
        using HttpResponseMessage response = await httpClient.DeleteAsync(uri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleTemplateInventoryResponse, cancellationToken);
    }

    private static Uri BuildTemplateUri(Guid templateId) => SimpleUriBuilder.Create(TEMPLATES_PATH)
        .AppendPath(templateId.ToString("D"))
        .BuildUri(UriKind.Relative);

    private static void ValidateTemplateId(Guid templateId)
    {
        if (templateId == Guid.Empty)
        {
            throw new ArgumentException("Rule template ID must not be empty.", nameof(templateId));
        }
    }
}
