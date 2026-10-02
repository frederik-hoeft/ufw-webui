namespace Ufw.Roslyn.Controllers.Internals;

public static class Activator
{
    public static TController CreateController<TController>(IServiceProvider serviceProvider)
        where TController : ControllerBase
    {
        ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));
        if (serviceProvider.GetService(typeof(TController)) is not TController controller)
        {
            throw new InvalidOperationException($"Failed to activate controller of type {typeof(TController).FullName}. No such controller was registered in the service provider.");
        }
        return controller;
    }
}
