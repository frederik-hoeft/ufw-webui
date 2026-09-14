using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Systemd.Firewall;

internal sealed class FirewallRuleCapabilityValidator : IFirewallRuleCapabilityValidator
{
    public IResponsePayload? Validate(FirewallRuleSpecification rule, FirewallConfigurationSnapshot configuration)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(configuration);

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(rule);
        if (normalized.AddressFamily != FirewallAddressFamily.IPv6 || configuration.IPv6Enabled)
        {
            return null;
        }

        return new ModelValidationErrorResponse([
            new ModelValidationError(
                nameof(FirewallRuleSpecification.AddressFamily),
                "IPv6 rules are unavailable because IPv6 support is disabled in the current UFW configuration."),
        ]);
    }
}
