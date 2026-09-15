using Microsoft.Extensions.Localization;
using Ufw.Client.Localization;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Client.Rules.Authoring;

internal sealed class RuleEditorValidationService(IRuleValidationMessageLocalizer validationMessages, IStringLocalizer<ValidationStrings> validationText) : IRuleEditorValidationService
{
    public IReadOnlyList<string> Validate(FirewallRuleSpecification specification, string propertyName, bool ipv6Enabled)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        int separator = propertyName.LastIndexOf('.');
        string memberName = separator < 0 ? propertyName : propertyName[(separator + 1)..];
        List<ModelValidationError> errors = [.. RuleSpecificationValidator.Validate(specification)];
        if (!ipv6Enabled)
        {
            AddIPv6CapabilityErrors(specification, errors);
        }

        return errors
            .Where(error => string.Equals(error.PropertyName, memberName, StringComparison.Ordinal))
            .Select(validationMessages.Localize)
            .ToArray();
    }

    private void AddIPv6CapabilityErrors(FirewallRuleSpecification specification, List<ModelValidationError> errors)
    {
        string message = validationText["Ipv6Disabled"];
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
}
