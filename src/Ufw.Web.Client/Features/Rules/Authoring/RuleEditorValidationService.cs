using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal sealed class RuleEditorValidationService(IRuleValidationMessageLocalizer validationMessages) : IRuleEditorValidationService
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

    private static void AddIPv6CapabilityErrors(FirewallRuleSpecification specification, List<ModelValidationError> errors)
    {
        const string message = "IPv6 rules are unavailable because IPv6 support is disabled in the current UFW configuration.";
        if (specification.AddressFamily == FirewallAddressFamily.IPv6)
        {
            errors.Add(new ModelValidationError(nameof(FirewallRuleSpecification.AddressFamily), message, FirewallRuleValidationErrorCodes.IPV6_DISABLED));
        }
        if (RuleSpecificationNormalizer.GetAddressFamily(specification.Source) == FirewallAddressFamily.IPv6)
        {
            errors.Add(new ModelValidationError(nameof(FirewallRuleSpecification.Source), message, FirewallRuleValidationErrorCodes.IPV6_DISABLED));
        }
        if (RuleSpecificationNormalizer.GetAddressFamily(specification.Destination) == FirewallAddressFamily.IPv6)
        {
            errors.Add(new ModelValidationError(nameof(FirewallRuleSpecification.Destination), message, FirewallRuleValidationErrorCodes.IPV6_DISABLED));
        }
    }
}
