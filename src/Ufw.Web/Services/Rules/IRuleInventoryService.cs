using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Services.Rules;

public interface IRuleInventoryService
{
    Task<RuleInventoryResponse> GetAsync(CancellationToken cancellationToken = default);
}
