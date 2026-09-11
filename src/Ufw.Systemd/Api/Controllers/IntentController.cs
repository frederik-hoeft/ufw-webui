using Ufw.Roslyn.Controllers;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Api.Controllers;

internal sealed partial class IntentController(IDeploymentIdentityProvider deploymentIdentity) : ControllerBase
{
    public partial ValueTask<IntentContextResponse> GetContextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new IntentContextResponse(IntentProtocol.VERSION, deploymentIdentity.GetDeploymentId()));
    }
}
