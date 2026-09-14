using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Client.Api;
using Ufw.Client.KnownHosts;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Client.Components.Rules;

public sealed partial class RuleEditor
{
    private MudForm? _form;
    private IReadOnlyList<KnownHostInventoryItem> _knownHosts = [];
    private IReadOnlyList<KnownHostInventoryItem> _visibleKnownHosts = [];
    private IReadOnlyList<NetworkInterfaceInventoryItem> _knownInterfaces = [];
    private IReadOnlyList<NetworkInterfaceInventoryItem> _visibleInterfaces = [];
    private string? _interfaceInventoryError;
    private bool _isValid;

    private Func<object, string, IEnumerable<string>> RuleValidation => ValidateRule;

    private bool RequiresPrivateKey => ShowAuthorization && string.IsNullOrWhiteSpace(PrivateKey);

    private string EffectiveDefinitionDescription => string.IsNullOrWhiteSpace(DefinitionDescription)
        ? RulesText["DefinitionDescription"]
        : DefinitionDescription;

    private string EffectiveSubmitLabel => string.IsNullOrWhiteSpace(SubmitLabel)
        ? RulesText["AddSignedRule"]
        : SubmitLabel;

    private string EffectiveSubmittingLabel => string.IsNullOrWhiteSpace(SubmittingLabel)
        ? RulesText["AddingRule"]
        : SubmittingLabel;

    private string SourceInterfaceHelp => DescribeInterfaceHelp(RulesText["SourceInterfaceHelp"]);

    private string DestinationInterfaceHelp => DescribeInterfaceHelp(RulesText["DestinationInterfaceHelp"]);

    private bool HasUnknownInterfaces =>
        IsUnknownInterface(Rule.SourceInterface) || IsUnknownInterface(Rule.DestinationInterface);

    [Parameter, EditorRequired]
    public FirewallRuleSpecification Rule { get; set; } = null!;

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
        await Task.WhenAll(LoadKnownHostsAsync(), LoadNetworkInterfacesAsync());
    }

    protected override void OnParametersSet() => RefreshVisibleKnownHosts();

    private async Task LoadKnownHostsAsync()
    {
        try
        {
            KnownHostInventoryResponse inventory = await KnownHosts.RefreshAsync();
            _knownHosts = inventory.Hosts;
            RefreshVisibleKnownHosts();
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _ = ClientErrors.Describe(exception);
            _knownHosts = [];
            _visibleKnownHosts = [];
        }
    }

    private async Task LoadNetworkInterfacesAsync()
    {
        try
        {
            NetworkInterfaceInventoryResponse inventory = await NetworkInterfaces.RefreshAsync();
            _knownInterfaces = inventory.Interfaces;
            _visibleInterfaces = inventory.Interfaces.Where(static networkInterface => networkInterface.IsVisible).ToArray();
        }
        catch (Exception exception)
        {
            _interfaceInventoryError = ClientErrors.Describe(exception).Message;
        }
    }

    private FirewallAddressFamily SourceKnownHostAddressFamily =>
        KnownHostSuggestions.ResolveCompatibleAddressFamily(Rule.AddressFamily, Rule.Destination);

    private FirewallAddressFamily DestinationKnownHostAddressFamily =>
        KnownHostSuggestions.ResolveCompatibleAddressFamily(Rule.AddressFamily, Rule.Source);

    private bool IsUnknownInterface(string? interfaceName) =>
        _interfaceInventoryError is null
        && !string.IsNullOrWhiteSpace(interfaceName)
        && !_knownInterfaces.Any(candidate => string.Equals(candidate.Name, interfaceName, StringComparison.Ordinal));

    private string DescribeInterfaceHelp(string directionHelp)
    {
        if (_interfaceInventoryError is not null)
        {
            return RulesText["InterfaceSuggestionsUnavailable", directionHelp];
        }

        return _visibleInterfaces.Count == 0
            ? RulesText["InterfaceEnterValid", directionHelp]
            : RulesText["InterfaceChooseKnown", directionHelp];
    }

    private IEnumerable<string> ValidateRule(object model, string propertyName)
    {
        if (model is not FirewallRuleSpecification specification)
        {
            return [];
        }

        int separator = propertyName.LastIndexOf('.');
        string memberName = separator < 0 ? propertyName : propertyName[(separator + 1)..];
        List<ModelValidationError> errors = [.. RuleSpecificationValidator.Validate(specification)];
        if (!IPv6Enabled)
        {
            AddIPv6CapabilityErrors(specification, errors);
        }

        return errors
            .Where(error => string.Equals(error.PropertyName, memberName, StringComparison.Ordinal))
            .Select(ValidationMessages.Localize);
    }

    private void RefreshVisibleKnownHosts()
    {
        _visibleKnownHosts = _knownHosts
            .Where(host => host.IsVisible && (IPv6Enabled || host.AddressFamily != FirewallAddressFamily.IPv6))
            .ToArray();
    }

    private void AddIPv6CapabilityErrors(FirewallRuleSpecification specification, List<ModelValidationError> errors)
    {
        string message = ValidationText["Ipv6Disabled"];
        if (specification.AddressFamily == FirewallAddressFamily.IPv6)
        {
            errors.Add(new ModelValidationError(nameof(FirewallRuleSpecification.AddressFamily), message));
        }
        if (RuleSpecificationNormalizer.GetAddressFamily(specification.Source) == FirewallAddressFamily.IPv6)
        {
            errors.Add(new ModelValidationError(nameof(FirewallRuleSpecification.Source), message));
        }
        if (RuleSpecificationNormalizer.GetAddressFamily(specification.Destination) == FirewallAddressFamily.IPv6)
        {
            errors.Add(new ModelValidationError(nameof(FirewallRuleSpecification.Destination), message));
        }
    }

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
