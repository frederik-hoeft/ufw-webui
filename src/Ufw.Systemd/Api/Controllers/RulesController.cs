using Ufw.Ipc.Shared.Model.Responses.Domain;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Routing;

namespace Ufw.Systemd.Api.Controllers;

[Route("api/v1/rules")]
internal sealed class RulesController() : ControllerBase
{
    [Get("list")]
    public async ValueTask<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken)
    {
        // TODO: placeholder implementation
        await Task.Yield();
        return new RuleListResponse();
    }
}
