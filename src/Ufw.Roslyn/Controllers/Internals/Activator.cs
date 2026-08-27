using Ufw.Roslyn.Controllers.Mapping.Delegates;

namespace Ufw.Roslyn.Controllers.Internals;

public static class Activator
{
    public static async ValueTask<TController> CreateControllerAsync<TController>(IServiceProvider serviceProvider, ControllerInitializationTask initializeAsync, CancellationToken cancellationToken)
        where TController : ControllerBase
    {
        ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));
        ArgumentNullException.ThrowIfNull(initializeAsync, nameof(initializeAsync));
        if (serviceProvider.GetService(typeof(TController)) is not TController controller)
        {
            throw new InvalidOperationException($"Failed to activate controller of type {typeof(TController).FullName}. No such controller was registered in the service provider.");
        }

        await initializeAsync(serviceProvider, controller, cancellationToken);
        TController controllerAsync = controller;
        return controllerAsync;
    }
}
