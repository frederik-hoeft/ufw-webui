using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Shared.Firewall;

/// <summary>
/// Validates and canonicalizes literal firewall host or network addresses outside a complete rule specification.
/// </summary>
public static class FirewallAddressValue
{
    public static bool TryNormalizeLiteral(
        string? address,
        [NotNullWhen(true)] out string? normalized,
        out FirewallAddressFamily addressFamily)
    {
        FirewallRuleSpecification probe = new() { Source = address };
        ModelValidationError[] errors = RuleSpecificationValidator.Validate(probe);
        if (errors.Any(static error => string.Equals(error.PropertyName, nameof(FirewallRuleSpecification.Source), StringComparison.Ordinal)))
        {
            normalized = null;
            addressFamily = FirewallAddressFamily.Any;
            return false;
        }

        string candidate = RuleSpecificationNormalizer.NormalizeAddress(address);
        addressFamily = RuleSpecificationNormalizer.GetAddressFamily(candidate);
        if (candidate == RuleSpecificationNormalizer.ANY || addressFamily == FirewallAddressFamily.Any)
        {
            normalized = null;
            addressFamily = FirewallAddressFamily.Any;
            return false;
        }

        normalized = candidate;
        return true;
    }
}
