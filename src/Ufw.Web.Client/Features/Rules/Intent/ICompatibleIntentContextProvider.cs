namespace Ufw.Web.Client.Features.Rules.Intent;

internal interface ICompatibleIntentContextProvider
{
    Task<string> GetDeploymentIdAsync(CancellationToken cancellationToken = default);
}
