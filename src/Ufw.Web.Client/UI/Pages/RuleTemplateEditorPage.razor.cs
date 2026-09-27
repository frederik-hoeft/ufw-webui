using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Templates;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.UI.Components.Rules.Metadata;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class RuleTemplateEditorPage
{
    private readonly CancellationTokenSource _lifetime = new();
    private MudForm? _detailsForm;
    private RuleMetadataEditor? _metadataEditor;
    private RuleTemplateDraft? _draft;
    private RuleMetadataEditorResult _metadataDraft = RuleMetadataEditorResult.Empty;
    private ClientError? _error;
    private Guid? _loadedRouteId;
    private bool _routeInitialized;
    private bool _loaded;
    private bool _notFound;
    private bool _errorFromSave;
    private bool _saving;

    [Parameter]
    public Guid? TemplateId { get; set; }

    private bool IsEdit => TemplateId.HasValue;

    private string PageTitleText => IsEdit ? TemplatesText["EditPageTitle"] : TemplatesText["CreatePageTitle"];

    private string TitleText => IsEdit ? TemplatesText["EditTitle"] : TemplatesText["CreateTitle"];

    private string DescriptionText => IsEdit ? TemplatesText["EditDescription"] : TemplatesText["CreateDescription"];

    private string ErrorTitle => _notFound
        ? TemplatesText["TemplateNotFoundTitle"]
        : _errorFromSave ? TemplatesText["SaveFailed"] : TemplatesText["LoadFailed"];

    private string? ErrorDescription => _notFound ? TemplatesText["TemplateNotFoundDescription"] : _error?.Message;

    private Func<string?, string?> ValidateName => value => string.IsNullOrWhiteSpace(value) ? TemplatesText["NameRequired"].Value : null;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(TemplatesText["TemplatesBreadcrumb"], "/templates"),
        new BreadcrumbItem(TitleText, null, disabled: true),
    ];

    protected async override Task OnParametersSetAsync()
    {
        if (_routeInitialized && _loadedRouteId == TemplateId)
        {
            return;
        }

        _routeInitialized = true;
        _loadedRouteId = TemplateId;
        await LoadAsync();
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task LoadAsync()
    {
        _loaded = false;
        _notFound = false;
        _error = null;
        _errorFromSave = false;
        _draft = null;
        _metadataDraft = RuleMetadataEditorResult.Empty;

        if (TemplateId is not { } templateId)
        {
            _draft = TemplateDraftFactory.Create();
            _loaded = true;
            return;
        }

        try
        {
            IReadOnlyList<RuleTemplate> templates = await TemplateCatalog.RefreshAsync(_lifetime.Token);
            RuleTemplate? template = templates.SingleOrDefault(candidate => candidate.Id == templateId);
            if (template is null)
            {
                _draft = null;
                _notFound = true;
                return;
            }

            _draft = TemplateDraftFactory.CreateFromExisting(template);
            _metadataDraft = new RuleMetadataEditorResult(_draft.Notes, _draft.TagIds, _draft.GroupId);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
        }
        finally
        {
            _loaded = true;
        }
    }

    private async Task SaveAsync()
    {
        if (_saving || _draft is null || _detailsForm is null || _metadataEditor is null)
        {
            return;
        }

        await _detailsForm.ValidateAsync();
        if (!_detailsForm.IsValid || !await _metadataEditor.ValidateAsync())
        {
            return;
        }

        RuleMetadataEditorResult metadata = _metadataDraft.Normalize();
        FirewallRuleSpecification rule = RuleSpecificationNormalizer.Normalize(_draft.Rule);
        RuleTemplateDefinition definition = new(
            _draft.Name.Trim(),
            string.IsNullOrWhiteSpace(_draft.Description) ? null : _draft.Description.Trim(),
            rule,
            metadata.Notes,
            metadata.TagIds,
            metadata.GroupId);

        _saving = true;
        _error = null;
        _errorFromSave = false;
        try
        {
            if (TemplateId is { } templateId)
            {
                _ = await TemplateCatalog.UpdateAsync(templateId, definition, _lifetime.Token);
                Snackbar.Add(TemplatesText["TemplateSaved"], Severity.Success);
            }
            else
            {
                _ = await TemplateCatalog.CreateAsync(definition, _lifetime.Token);
                Snackbar.Add(TemplatesText["TemplateCreated"], Severity.Success);
            }
            Navigation.NavigateTo("/templates");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
            _errorFromSave = true;
            Snackbar.Add(_error.Message, Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private Task MetadataChanged(RuleMetadataEditorResult value)
    {
        _metadataDraft = value;
        return Task.CompletedTask;
    }

    private void Cancel() => Navigation.NavigateTo("/templates");
}
