using Ufw.Shared.Management.KnownHosts;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Client.Api.NetworkInterfaces;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Client.Features.Rules.Authoring;

using Ufw.Shared.Management.NetworkInterfaces;
namespace Ufw.Web.Client.UI.Components.Rules;

public sealed partial class RuleEditor
{
    private readonly CancellationTokenSource _lifetime = new();
    private MudForm? _form;
    private RuleEditorReferenceData _referenceData = new([], [], [], null);
    private IReadOnlyList<KnownHostInventoryItem> _visibleKnownHosts = [];
    private bool _isValid;

    private Func<object, string, IEnumerable<string>> RuleValidation => ValidateRule;

    private bool RequiresPrivateKey => ShowAuthorization && string.IsNullOrWhiteSpace(PrivateKey);

    private string EffectiveDefinitionDescription => string.IsNullOrWhiteSpace(DefinitionDescription)
        ? Mode == RuleEditorMode.Edit ? RulesText["EditDefinitionDescription"] : RulesText["DefinitionDescription"]
        : DefinitionDescription;

    private string EffectiveSubmitLabel => string.IsNullOrWhiteSpace(SubmitLabel)
        ? Mode == RuleEditorMode.Edit ? RulesText["UpdateSignedRule"] : RulesText["AddSignedRule"]
        : SubmitLabel;

    private string EffectiveSubmittingLabel => string.IsNullOrWhiteSpace(SubmittingLabel)
        ? Mode == RuleEditorMode.Edit ? RulesText["UpdatingRule"] : RulesText["AddingRule"]
        : SubmittingLabel;

    private string EffectiveSubmitIcon => string.IsNullOrWhiteSpace(SubmitIcon)
        ? Mode == RuleEditorMode.Edit ? Icons.Material.Filled.Save : Icons.Material.Filled.Add
        : SubmitIcon;

    private bool EffectiveAddressFamilyLocked => AddressFamilyLocked || Mode == RuleEditorMode.Edit;

    private string SourceInterfaceHelp => DescribeInterfaceHelp(RulesText["SourceInterfaceHelp"]);

    private string DestinationInterfaceHelp => DescribeInterfaceHelp(RulesText["DestinationInterfaceHelp"]);

    private bool HasUnknownInterfaces => IsUnknownInterface(Rule.SourceInterface) || IsUnknownInterface(Rule.DestinationInterface);

    private IReadOnlyList<NetworkInterfaceInventoryItem> VisibleInterfaces => _referenceData.VisibleInterfaces;

    private FirewallAddressFamily SourceKnownHostAddressFamily => HostSuggestions.ResolveCompatibleAddressFamily(Rule.AddressFamily, Rule.Destination);

    private FirewallAddressFamily DestinationKnownHostAddressFamily => HostSuggestions.ResolveCompatibleAddressFamily(Rule.AddressFamily, Rule.Source);

    [Parameter, EditorRequired]
    public FirewallRuleSpecification Rule { get; set; } = null!;

    [Parameter]
    public RuleEditorMode Mode { get; set; } = RuleEditorMode.Create;

    [Parameter]
    public FirewallRuleSpecification? OriginalRule { get; set; }

    [Parameter]
    public string? DefinitionDescription { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool AddressFamilyLocked { get; set; }

    [Parameter]
    public bool IPv6Enabled { get; set; }

    [Parameter]
    public bool SubmitDisabled { get; set; }

    [Parameter]
    public bool ShowAuthorization { get; set; } = true;

    [Parameter]
    public string? SubmitLabel { get; set; }

    [Parameter]
    public string? SubmittingLabel { get; set; }

    [Parameter]
    public string? SubmitIcon { get; set; }

    [Parameter]
    public bool Submitting { get; set; }

    [Parameter]
    public string PrivateKey { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> PrivateKeyChanged { get; set; }

    [Parameter]
    public EventCallback OnSubmit { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    protected async override Task OnInitializedAsync()
    {
        try
        {
            _referenceData = await ReferenceDataService.LoadAsync(_lifetime.Token);
            RefreshVisibleKnownHosts();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    protected override void OnParametersSet()
    {
        if (Mode == RuleEditorMode.Edit && OriginalRule is null)
        {
            throw new InvalidOperationException("Edit-mode rule authoring requires the original rule for comparison.");
        }

        RefreshVisibleKnownHosts();
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private bool IsUnknownInterface(string? interfaceName) => ReferenceDataService.IsUnknownInterface(_referenceData, interfaceName);

    private string DescribeInterfaceHelp(string directionHelp)
    {
        if (_referenceData.InterfaceInventoryError is not null)
        {
            return RulesText["InterfaceSuggestionsUnavailable", directionHelp];
        }

        return VisibleInterfaces.Count == 0
            ? RulesText["InterfaceEnterValid", directionHelp]
            : RulesText["InterfaceChooseKnown", directionHelp];
    }

    private IEnumerable<string> ValidateRule(object model, string propertyName) =>
        model is FirewallRuleSpecification specification ? EditorValidation.Validate(specification, propertyName, IPv6Enabled) : [];

    private void RefreshVisibleKnownHosts() => _visibleKnownHosts = ReferenceDataService.GetVisibleKnownHosts(_referenceData, IPv6Enabled);

    private async Task DirectionChangedAsync(FirewallDirection value)
    {
        Rule.Direction = value;

        if (_form is not null)
        {
            await _form.ValidateAsync();
        }
    }

    private async Task PrivateKeyChangedAsync(string value)
    {
        PrivateKey = value;
        await PrivateKeyChanged.InvokeAsync(value);
    }

    private async Task CancelAsync()
    {
        PrivateKey = string.Empty;
        await PrivateKeyChanged.InvokeAsync(string.Empty);
        await OnCancel.InvokeAsync();
    }

    private async Task SubmitAsync()
    {
        if (_form is null || Disabled || SubmitDisabled || Submitting || RequiresPrivateKey)
        {
            return;
        }

        await _form.ValidateAsync();
        if (_form.IsValid)
        {
            await OnSubmit.InvokeAsync();
        }
    }
}
