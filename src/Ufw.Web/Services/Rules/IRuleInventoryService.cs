using Ufw.Web.Api.V1.Models.Rules;

namespace Ufw.Web.Services.Rules;

public interface IRuleInventoryService
{
    Task<RuleInventoryResponse> GetAsync(CancellationToken cancellationToken = default);
}
