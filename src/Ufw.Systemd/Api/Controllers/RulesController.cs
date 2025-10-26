using Ufw.Pipes.Shared.Model;
using Ufw.Pipes.Shared.Model.Requests.Domain;
using Ufw.Pipes.Shared.Model.Responses;
using Ufw.Pipes.Shared.Model.Responses.Domain;
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

    [Delete]
    public async ValueTask<IResponseMessage> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken)
    {
        if (request.RuleId is null)
        {
            return new BadRequestResponse("RuleId is required");
        }
        // TODO: placeholder implementation
        await Task.Yield();
        return new OkResponse();
    }
}
