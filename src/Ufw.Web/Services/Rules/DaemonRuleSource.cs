using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Services.Rules;

internal sealed class DaemonRuleSource(IUfwClient ufwClient) : IDaemonRuleSource
{
    public Task<RuleListResponse> GetAsync(CancellationToken cancellationToken = default) =>
        ufwClient.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", cancellationToken);
}
