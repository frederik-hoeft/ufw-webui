using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Services.Rules;

internal interface IDaemonRuleSource
{
    Task<RuleListResponse> GetAsync(CancellationToken cancellationToken = default);
}
