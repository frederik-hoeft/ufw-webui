namespace Ufw.Roslyn.Controllers.Mapping.Delegates;

public delegate ValueTask ControllerInitializationTask(IServiceProvider serviceProvider, ControllerBase controller, CancellationToken cancellationToken);
