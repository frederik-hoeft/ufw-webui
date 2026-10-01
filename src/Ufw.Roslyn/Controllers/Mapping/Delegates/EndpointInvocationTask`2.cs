namespace Ufw.Roslyn.Controllers.Mapping.Delegates;

public delegate ValueTask<TResponse> EndpointInvocationTask<in TRequest, TResponse>(IServiceProvider serviceProvider, TRequest request, CancellationToken cancellationToken)
    where TResponse : IIdentifiable;
