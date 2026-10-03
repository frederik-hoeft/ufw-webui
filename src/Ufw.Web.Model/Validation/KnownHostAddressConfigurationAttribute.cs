using System.ComponentModel.DataAnnotations;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Model.Validation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KnownHostAddressConfigurationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        if (value is null)
        {
            return ValidationResult.Success;
        }
        if (value is not KnownHostRequest request)
        {
            return new ValidationResult("The known-host request has an invalid value type.");
        }

        return request.AddressSource switch
        {
            KnownHostAddressSource.Literal => ValidateLiteral(request),
            KnownHostAddressSource.Dns => ValidateDns(request),
            _ => new ValidationResult("The address source is invalid.", [nameof(request.AddressSource)]),
        };
    }

    private static ValidationResult? ValidateLiteral(KnownHostRequest request)
    {
        if (request.DnsAddressFamily is not null)
        {
            return new ValidationResult("DNS address family must be omitted for literal known hosts.", [nameof(request.DnsAddressFamily)]);
        }
        if (request.Address is not null && !string.Equals(request.Address, request.Address.Trim(), StringComparison.Ordinal))
        {
            return new ValidationResult("Address must not contain leading or trailing whitespace.", [nameof(request.Address)]);
        }
        if (!FirewallAddressValue.TryNormalizeLiteral(request.Address, out _, out _))
        {
            return new ValidationResult("Address must be a valid IPv4 or IPv6 host/network address.", [nameof(request.Address)]);
        }
        return ValidationResult.Success;
    }

    private static ValidationResult? ValidateDns(KnownHostRequest request)
    {
        if (request.Address is not null)
        {
            return new ValidationResult("Address must be omitted for DNS-managed known hosts.", [nameof(request.Address)]);
        }
        if (request.DnsAddressFamily is not FirewallAddressFamily.IPv4 and not FirewallAddressFamily.IPv6)
        {
            return new ValidationResult("DNS address family must be IPv4 or IPv6 for DNS-managed known hosts.", [nameof(request.DnsAddressFamily)]);
        }
        return ValidationResult.Success;
    }
}
