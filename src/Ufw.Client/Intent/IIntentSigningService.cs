using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;

namespace Ufw.Client.Intent;

public interface IIntentSigningService
{
    Task<AddRuleRequest> CreateAddRuleRequestAsync(string deploymentId, FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default);

    Task<DeleteRuleRequest> CreateDeleteRuleRequestAsync(string deploymentId, string ruleId, FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default);
}
