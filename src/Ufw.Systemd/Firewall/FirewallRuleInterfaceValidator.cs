using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.NetworkInterfaces;

namespace Ufw.Systemd.Firewall;

internal sealed class FirewallRuleInterfaceValidator(INetworkInterfaceSnapshotService networkInterfaces) : IFirewallRuleInterfaceValidator
{
    public IResponsePayload? Validate(FirewallRuleSpecification rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (string.IsNullOrWhiteSpace(rule.SourceInterface) && string.IsNullOrWhiteSpace(rule.DestinationInterface))
        {
            return null;
        }

        NetworkInterfaceSnapshot snapshot = networkInterfaces.GetSnapshot();
        if (!snapshot.IsAvailable)
        {
            return new InternalServerErrorResponse("Failed to validate rule interfaces against the current host network state.");
        }

        HashSet<string> available = new(snapshot.Interfaces, StringComparer.Ordinal);
        List<ModelValidationError> errors = [];
        if (!string.IsNullOrWhiteSpace(rule.SourceInterface) && !available.Contains(rule.SourceInterface))
        {
            errors.Add(new ModelValidationError(nameof(FirewallRuleSpecification.SourceInterface), $"Interface '{rule.SourceInterface}' is not present on this host."));
        }

        if (!string.IsNullOrWhiteSpace(rule.DestinationInterface) && !available.Contains(rule.DestinationInterface))
        {
            errors.Add(new ModelValidationError(nameof(FirewallRuleSpecification.DestinationInterface), $"Interface '{rule.DestinationInterface}' is not present on this host."));
        }

        return errors.Count == 0 ? null : new ModelValidationErrorResponse([.. errors]);
    }
}
