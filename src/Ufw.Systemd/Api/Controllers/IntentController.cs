using Ufw.Ipc.Shared.Model.Responses.Domain;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Routing;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Api.Controllers;

[Route("api/v1/intent")]
internal sealed class IntentController(IDeploymentIdentityProvider deploymentIdentity) : ControllerBase
{
    [Get("context")]
    public ValueTask<IntentContextResponse> GetContextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new IntentContextResponse(IntentProtocol.VERSION, deploymentIdentity.GetDeploymentId()));
    }
}
