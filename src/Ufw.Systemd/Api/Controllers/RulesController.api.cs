using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Routing;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;

namespace Ufw.Systemd.Api.Controllers;

[Route("api/v1/rules")]
internal sealed partial class RulesController
{
    /// <summary>
    /// Returns the daemon-authoritative UFW rule snapshot.
    /// </summary>
    [Get]
    public partial ValueTask<IResponsePayload> GetRulesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Validates and applies an administrator-signed add-rule intent.
    /// </summary>
    [Post]
    public partial ValueTask<IResponsePayload> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Validates and applies an administrator-signed delete-rule intent.
    /// </summary>
    [Delete]
    public partial ValueTask<IResponsePayload> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken);
}
