namespace Ufw.Roslyn.Controllers.Mapping.Delegates;

public delegate ValueTask<TResponse> EndpointInvocationTask<TResponse>(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    where TResponse : IIdentifiable;
