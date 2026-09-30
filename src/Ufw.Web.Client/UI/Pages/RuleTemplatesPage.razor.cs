using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Ufw.Web.Client.Features.Rules.Templates;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.UI.Components.Rules.Templates;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class RuleTemplatesPage
{
    private static readonly DialogOptions s_deleteDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.ExtraSmall,
    };

    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<Guid> _expandedTemplateIds = [];
    private IReadOnlyList<RuleTemplate> _templates = [];
    private ClientError? _error;
    private bool _loaded;
    private bool _loading;
    private bool _saving;
    private bool _mutationError;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs => [new BreadcrumbItem(TemplatesText["TemplatesBreadcrumb"], null, disabled: true)];

    private bool IsBusy => _loading || _saving;

    protected override Task OnInitializedAsync() => RefreshAsync();

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _loading = true;
        _error = null;
        _mutationError = false;
        try
        {
            _templates = await TemplateCatalog.RefreshAsync(_lifetime.Token);
            RetainExpandedTemplates();
            _loaded = true;
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
            _loading = false;
        }
    }

    private void Create() => Navigation.NavigateTo("/templates/create");

    private void Use(RuleTemplate template) => Navigation.NavigateTo($"/rules/create?template={template.Id:D}");

    private void Edit(RuleTemplate template) => Navigation.NavigateTo($"/templates/edit/{template.Id:D}");

    private async Task DeleteAsync(RuleTemplate template)
    {
        if (IsBusy)
        {
            return;
        }

        DialogParameters<DeleteRuleTemplateDialog> parameters = [];
        parameters.Add(component => component.Template, template);
        IDialogReference dialog = await DialogService.ShowAsync<DeleteRuleTemplateDialog>(TemplatesText["DeleteDialogTitle"], parameters, s_deleteDialogOptions);
        if (await dialog.GetReturnValueAsync<bool?>() != true)
        {
            return;
        }

        _saving = true;
        _error = null;
        _mutationError = false;
        try
        {
            _templates = await TemplateCatalog.DeleteAsync(template.Id, _lifetime.Token);
            RetainExpandedTemplates();
            _loaded = true;
            Snackbar.Add(TemplatesText["TemplateDeleted"], Severity.Success);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception);
            _mutationError = true;
            Snackbar.Add(_error.Message, Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private string DescribeRule(RuleTemplate template) => RuleRenderer.Render(template.Rule).DisplayText;

    private void RetainExpandedTemplates()
    {
        HashSet<Guid> current = [.. _templates.Select(static template => template.Id)];
        _expandedTemplateIds.RemoveWhere(id => !current.Contains(id));
    }

    private bool IsExpanded(Guid id) => _expandedTemplateIds.Contains(id);

    private void ToggleExpanded(Guid id)
    {
        if (!_expandedTemplateIds.Add(id))
        {
            _expandedTemplateIds.Remove(id);
        }
    }

    private void HandleKeyDown(KeyboardEventArgs args, Guid id)
    {
        if (args.Key is "Enter" or " ")
        {
            ToggleExpanded(id);
        }
    }

    private string DescribeMetadata(RuleTemplate template)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(template.Notes))
        {
            parts.Add(TemplatesText["NotesPresent"]);
        }
        if (template.Tags.Count > 0)
        {
            parts.Add(template.Tags.Count == 1 ? TemplatesText["OneTag"] : TemplatesText["ManyTags", template.Tags.Count]);
        }
        if (template.Group is not null)
        {
            parts.Add(TemplatesText["GroupPresent"]);
        }
        return parts.Count == 0 ? TemplatesText["NoMetadata"] : string.Join(", ", parts);
    }

    private string DescribeTemplateCount(int count) => count == 1
        ? TemplatesText["TemplateCountOne"]
        : TemplatesText["TemplateCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];
}
