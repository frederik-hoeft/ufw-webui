using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Features.Rules.Authoring.Workflows;
using Ufw.Web.Client.Features.Rules.Insertion;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Templates;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.UI.Components.Rules.Metadata;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class CreateRule
{
    private readonly CancellationTokenSource _lifetime = new();
    private RuleInventoryState _state = RuleInventoryState.Initial;
    private RuleCreationInteractionState _interaction = RuleCreationInteractionState.Initial;
    private FirewallRuleSpecification _draft = null!;
    private RuleMetadataEditor? _metadataEditor;
    private RuleMetadataEditorResult _metadataDraft = RuleMetadataEditorResult.Empty;
    private IReadOnlyList<RuleTemplate> _templates = [];
    private Guid? _selectedTemplateId;
    private Guid? _loadedTemplateId;
    private ClientError? _templateCatalogError;
    private string? _templateContextWarning;
    private string _privateKey = string.Empty;
    private bool _initialAddressFamilyApplied;
    private bool _initialTemplateHandled;
    private bool _templatesLoaded;
    private bool _loadingTemplates;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(RulesText["FirewallBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(RulesText["RulesBreadcrumb"], "/rules"),
        new BreadcrumbItem(RulesText["AddRuleBreadcrumb"], null, disabled: true),
    ];

    private RuleInsertionNavigationQuery InsertionQuery => new(InsertionBaselineFingerprint, InsertionAnchorValue, InsertionPlacementValue, LegacyBeforeRuleId, LegacyAfterRuleId);

    private bool HasLegacyInsertionTarget => InsertionQuery.HasLegacyTarget;

    private bool IsOrderedInsertionRequested => InsertionQuery.IsRequested;

    private bool CanEdit => _state.IsCurrent && _interaction.CanEdit(IsOrderedInsertionRequested);

    private bool CanSubmit => CanEdit;

    private bool CanLoadSelectedTemplate => CanEdit && !_loadingTemplates && _selectedTemplateId is not null;

    private string HeaderDescription => IsOrderedInsertionRequested
        ? RulesText["CreateOrderedDescription"]
        : RulesText["CreateDescription"];

    private string RuleDefinitionDescription => IsOrderedInsertionRequested
        ? RulesText["OrderedRuleDefinitionDescription"]
        : RulesText["DefinitionDescription"];

    [Parameter, SupplyParameterFromQuery(Name = "family")]
    public string? InitialAddressFamilyValue { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "template")]
    public string? InitialTemplateValue { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "baseline")]
    public string? InsertionBaselineFingerprint { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "anchor")]
    public string? InsertionAnchorValue { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "placement")]
    public string? InsertionPlacementValue { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "before")]
    public string? LegacyBeforeRuleId { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "after")]
    public string? LegacyAfterRuleId { get; set; }

    protected async override Task OnInitializedAsync()
    {
        _draft = RuleDraftFactory.Create();
        try
        {
            await LoadRulesAsync(RuleInventoryRefreshReason.Manual);
            await LoadTemplatesAsync();
        }
        finally
        {
            _interaction = _interaction.InitializationCompleted();
        }
    }

    public void Dispose()
    {
        _privateKey = string.Empty;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private Task RefreshAsync()
    {
        if (!_interaction.CanRefresh || _state.IsLoading)
        {
            return Task.CompletedTask;
        }

        return LoadRulesAsync(RuleInventoryRefreshReason.Manual);
    }

    private async Task LoadRulesAsync(RuleInventoryRefreshReason reason)
    {
        if (_state.IsLoading)
        {
            return;
        }

        _state = _state.MoveNext(new RuleInventoryTransition.RefreshStarted(reason));
        try
        {
            RuleSnapshot snapshot = await RuleInventory.GetAsync(_lifetime.Token);
            _state = _state.MoveNext(new RuleInventoryTransition.RefreshCompleted(snapshot));
            ApplyInitialAddressFamily(snapshot.Configuration);
            ResolveOrderedInsertionContext(RuleSnapshotFactory.ToFirewallResponse(snapshot));
            TryApplyInitialTemplate();

            if (_interaction.IsAwaitingConfirmation)
            {
                string pendingRuleId = _interaction.PendingRuleId!;
                bool isPresent = MutationReconciliation.IsPresent(snapshot, pendingRuleId);
                _interaction = _interaction.AddPresenceChecked(isPresent);
                if (isPresent)
                {
                    Navigation.NavigateTo("/rules");
                    return;
                }

                Snackbar.Add(RulesText["SubmittedRuleMissing"], Severity.Warning);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            _state = _state.MoveNext(new RuleInventoryTransition.RefreshFailed(ClientErrors.Describe(exception)));
        }
    }

    private Task ReloadTemplatesAsync() => LoadTemplatesAsync();

    private async Task LoadTemplatesAsync()
    {
        if (_loadingTemplates)
        {
            return;
        }

        _loadingTemplates = true;
        _templateCatalogError = null;
        try
        {
            _templates = await TemplateCatalog.RefreshAsync(_lifetime.Token);
            _templatesLoaded = true;
            TryApplyInitialTemplate();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            _templateCatalogError = ClientErrors.Describe(exception);
            if (!string.IsNullOrWhiteSpace(InitialTemplateValue))
            {
                _initialTemplateHandled = true;
            }
        }
        finally
        {
            _loadingTemplates = false;
        }
    }

    private void TryApplyInitialTemplate()
    {
        if (_initialTemplateHandled || string.IsNullOrWhiteSpace(InitialTemplateValue))
        {
            return;
        }
        if (!Guid.TryParse(InitialTemplateValue, out Guid templateId))
        {
            _initialTemplateHandled = true;
            _templateContextWarning = TemplatesText["TemplateContextInvalid"];
            return;
        }
        if (!_templatesLoaded || IsOrderedInsertionRequested && _interaction.InsertionContext is null)
        {
            return;
        }

        _initialTemplateHandled = true;
        RuleTemplate? template = FindTemplate(templateId);
        if (template is null)
        {
            _templateContextWarning = TemplatesText["TemplateContextMissing"];
            return;
        }

        _selectedTemplateId = template.Id;
        ApplyTemplate(template);
    }

    private void LoadSelectedTemplate()
    {
        if (!CanLoadSelectedTemplate || _selectedTemplateId is not { } templateId || FindTemplate(templateId) is not { } template)
        {
            return;
        }
        ApplyTemplate(template);
    }

    private void ApplyTemplate(RuleTemplate template)
    {
        FirewallAddressFamily? requiredFamily = IsOrderedInsertionRequested ? _interaction.InsertionContext?.AddressFamily : null;
        RuleTemplateInstantiationResult result = TemplateAuthoring.Initialize(template, requiredFamily);
        if (!result.Succeeded)
        {
            _templateContextWarning = result.Error switch
            {
                RuleTemplateInstantiationError.AddressFamilyMismatch when requiredFamily is { } family
                    => TemplatesText["TemplateFamilyMismatch", template.Name, RuleText.FormatAddressFamily(template.Rule.AddressFamily), RuleText.FormatAddressFamily(family)],
                _ => TemplatesText["TemplateContextInvalid"],
            };
            return;
        }

        RuleTemplateInstantiation instantiation = result.Instantiation!;
        _draft = instantiation.Rule;
        _metadataDraft = new RuleMetadataEditorResult(instantiation.Notes, instantiation.TagIds, instantiation.GroupId);
        _loadedTemplateId = template.Id;
        _selectedTemplateId = template.Id;
        _templateContextWarning = null;
    }

    private RuleTemplate? FindTemplate(Guid templateId) => _templates.SingleOrDefault(template => template.Id == templateId);

    private string DescribeTemplateOption(RuleTemplate template)
    {
        if (_templates.Count(candidate => string.Equals(candidate.Name, template.Name, StringComparison.OrdinalIgnoreCase)) == 1)
        {
            return template.Name;
        }

        // Names are display-only: add a stable short ID when more than one template uses the same label.
        return $"{template.Name} ({template.Id.ToString("N")[^8..]})";
    }

    private void ApplyInitialAddressFamily(FirewallConfigurationSnapshot configuration)
    {
        if (_initialAddressFamilyApplied)
        {
            return;
        }

        _initialAddressFamilyApplied = true;
        if (IsOrderedInsertionRequested)
        {
            return;
        }

        _draft.AddressFamily = InitialAddressFamilyValue?.Trim().ToUpperInvariant() switch
        {
            "IPV4" => FirewallAddressFamily.IPv4,
            "IPV6" when configuration.IPv6Enabled => FirewallAddressFamily.IPv6,
            _ => _draft.AddressFamily,
        };
    }

    private void ResolveOrderedInsertionContext(RuleListResponse snapshot)
    {
        if (!IsOrderedInsertionRequested || _interaction.InsertionInvalidated)
        {
            return;
        }

        RuleInsertionNavigationResolution resolution = InsertionNavigation.Resolve(snapshot, InsertionQuery);
        _interaction = _interaction.InsertionResolved(resolution);
        if (_interaction.InsertionContext is { } context)
        {
            _draft.AddressFamily = context.AddressFamily;
        }
    }

    private async Task SubmitRuleAsync()
    {
        if (!CanSubmit || string.IsNullOrWhiteSpace(_privateKey))
        {
            return;
        }

        // Claim the submission before awaiting metadata validation: another UI event cannot submit the same draft concurrently.
        _interaction = _interaction.ValidationStarted();
        try
        {
            if (_metadataEditor is not null)
            {
                bool metadataValid = await _metadataEditor.ValidateAsync();
                if (!metadataValid)
                {
                    return;
                }
            }

            _metadataDraft = _metadataDraft.Normalize();
            if (IsOrderedInsertionRequested)
            {
                _interaction = _interaction.SubmissionStarted();
                await InsertRuleAsync();
            }
            else
            {
                // Resolve identity before the mutation starts; a local validation failure cannot leave an uncertain write behind.
                string requestedRuleId = MutationReconciliation.GetRequestedIdentity(_draft);
                _interaction = _interaction.SubmissionStarted();
                await AddRuleAsync(requestedRuleId);
            }
        }
        finally
        {
            if (_interaction.Phase == RuleCreationPhase.Validating)
            {
                _interaction = _interaction.ValidationStopped();
            }
        }
    }

    private async Task AddRuleAsync(string requestedRuleId)
    {
        try
        {
            RuleCreationAddResult result;
            try
            {
                result = await CreationWorkflow.AddAsync(_draft, CurrentMetadataChange(), _privateKey, _lifetime.Token);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (ClientErrors.CanDescribe(exception))
            {
                ClientError error = ClientErrors.Describe(exception);
                _state = _state.MoveNext(new RuleInventoryTransition.MutationFailed(error));
                _interaction = _state.StaleReason == RuleSnapshotStaleReason.MutationOutcomeUnknown
                    ? _interaction.AddAwaitingConfirmation(requestedRuleId)
                    : _interaction.AddRejected();
                Snackbar.Add(error.Message, Severity.Error);
                return;
            }

            _interaction = _interaction.AddAwaitingConfirmation(result.ConfirmedRuleId);
            NotifyMetadataSaveFailure(result.MetadataError);
            await LoadRulesAsync(RuleInventoryRefreshReason.AfterMutation);
        }
        finally
        {
            _privateKey = string.Empty;
        }
    }

    private async Task InsertRuleAsync()
    {
        if (_interaction.InsertionContext is not { } context || _state.Snapshot is not { } snapshot)
        {
            throw new InvalidOperationException("Ordered insertion requires a resolved context and a current rule snapshot.");
        }

        try
        {
            RuleCreationInsertionResult result = await CreationWorkflow.InsertAsync(snapshot, context, _draft, CurrentMetadataChange(), _privateKey, _lifetime.Token);
            RuleInsertionResponse response = result.Firewall;
            _state = _state.MoveNext(new RuleInventoryTransition.InsertionCompleted(response, TimeProvider.GetUtcNow()));
            _interaction = _interaction.InsertionCompleted(response);

            if (response.Outcome == RuleInsertionOutcome.Completed)
            {
                NotifyMetadataSaveFailure(result.MetadataError);
                Snackbar.Add(RulesText["OrderedInsertionApplied"], Severity.Success);
                Navigation.NavigateTo("/rules");
                return;
            }

            if (MutationReconciliation.MustReselectInsertionAnchor(response, context))
            {
                _interaction = _interaction.InvalidateInsertion();
            }

            Severity severity = response.Outcome == RuleInsertionOutcome.StateUncertain
                ? Severity.Error
                : Severity.Warning;
            Snackbar.Add(response.Diagnostic ?? DescribeInsertionResultTitle(response.Outcome), severity);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.CanDescribe(exception))
        {
            ClientError error = ClientErrors.Describe(exception);
            _state = _state.MoveNext(new RuleInventoryTransition.MutationFailed(error));
            _interaction = _interaction.InsertionFailed();
            if (_state.IsStale)
            {
                _interaction = _interaction.InvalidateInsertion();
            }
            Snackbar.Add(error.Message, Severity.Error);
        }
        finally
        {
            _privateKey = string.Empty;
        }
    }

    private Task MetadataChanged(RuleMetadataEditorResult value)
    {
        _metadataDraft = value;
        return Task.CompletedTask;
    }

    private RuleMetadataChange CurrentMetadataChange() => new(_metadataDraft.Notes, _metadataDraft.TagIds, _metadataDraft.GroupId);

    private void NotifyMetadataSaveFailure(ClientError? error)
    {
        if (error is not null)
        {
            Snackbar.Add(RulesText["MetadataSaveAfterCreateFailedWithReason", error.Message], Severity.Warning);
        }
    }

    private void Cancel()
    {
        if (!_interaction.IsSubmitting)
        {
            _privateKey = string.Empty;
            Navigation.NavigateTo("/rules");
        }
    }

    private string DescribeInsertionTarget()
    {
        if (_interaction.InsertionContext is not { } context)
        {
            return DescribeInsertionContextError();
        }

        string position = RulesText["RulePosition", context.AnchorFamilyPosition.ToString("N0", CultureInfo.CurrentCulture)];
        string placement = context.Placement == RuleInsertionPlacement.Before
            ? RulesText["Before"]
            : RulesText["After"];
        return RulesText["InsertTargetDescription", placement, position];
    }

    private string DescribeInsertionContextError()
    {
        if (HasLegacyInsertionTarget)
        {
            return RulesText["OrderedInsertionLegacyContext"];
        }

        return _interaction.InsertionError switch
        {
            OrderedRuleInsertionContextError.StaleBaseline => RulesText["InsertionBaselineStale"],
            OrderedRuleInsertionContextError.AnchorUnavailable => RulesText["InsertionTargetMissing"],
            OrderedRuleInsertionContextError.CapabilityUnavailable => RulesText["InsertionIPv6Unavailable"],
            OrderedRuleInsertionContextError.InvalidFingerprint
                or OrderedRuleInsertionContextError.InvalidPlacement
                or OrderedRuleInsertionContextError.Incomplete => RulesText["InsertionContextInvalid"],
            _ => RulesText["InsertionTargetUnavailable"],
        };
    }

    private string DescribeInsertionFamily() => _interaction.InsertionContext is { } context
        ? RulesText["InsertionFamilyLocked", context.AddressFamily.ToString()]
        : string.Empty;

    private Severity InsertionResultSeverity => _interaction.InsertionResult?.Outcome switch
    {
        RuleInsertionOutcome.StaleBaseline or RuleInsertionOutcome.PreconditionFailed => Severity.Warning,
        RuleInsertionOutcome.StateUncertain => Severity.Error,
        _ => Severity.Info,
    };

    private string DescribeInsertionResultTitle() => _interaction.InsertionResult is { } result
        ? DescribeInsertionResultTitle(result.Outcome)
        : RulesText["OrderedInsertionResult"];

    private string DescribeInsertionResultTitle(RuleInsertionOutcome outcome) => outcome switch
    {
        RuleInsertionOutcome.Completed => RulesText["OrderedInsertionResultCompleted"],
        RuleInsertionOutcome.StaleBaseline => RulesText["OrderedInsertionResultStale"],
        RuleInsertionOutcome.PreconditionFailed => RulesText["OrderedInsertionResultPrecondition"],
        RuleInsertionOutcome.StateUncertain => RulesText["OrderedInsertionResultUncertain"],
        _ => RulesText["OrderedInsertionResult"],
    };

    private string DescribeStaleState() => _state.StaleReason switch
    {
        RuleSnapshotStaleReason.MutationCommitted => RulesText["RefreshAfterMutationFailed"],
        RuleSnapshotStaleReason.MutationOutcomeUnknown => RulesText["MutationOutcomeUnknown"],
        RuleSnapshotStaleReason.MutationRejectedRequiresRefresh => RulesText["MutationRejectedRefresh"],
        _ => RulesText["LatestRefreshFailed"],
    };
}
