namespace Ufw.Roslyn.Controllers.Mapping.Delegates;

public delegate ValueTask<TResponse> EndpointInvocationTask<TResponse>(IServiceProvider serviceProvider, ControllerInitializationTask initializeAsync, CancellationToken cancellationToken)
    where TResponse : IIdentifiable;
