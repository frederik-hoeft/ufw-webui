using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Requests.Domain;

namespace Ufw.Client.Intent;

public interface IIntentSigningService
{
    Task<AddRuleRequest> CreateAddRuleRequestAsync(
        string deploymentId,
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default);

    Task<DeleteRuleRequest> CreateDeleteRuleRequestAsync(
        string deploymentId,
        string ruleId,
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default);
}
