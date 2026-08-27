using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Systemd.Api.Framework;

namespace Ufw.Ipc.Tests.Adapter.Endpoints;

internal sealed class TestEndpointMapBuilder : ITestEndpointMapBuilder
{
    private readonly List<ApiEndpointMapping<IMessage, IMessage>> _mappings = [];

    public IReadOnlyList<ApiEndpointMapping<IMessage, IMessage>> Mappings => _mappings;

    public ITestEndpointMapBuilder Map(ApiEndpointMapping<IMessage, IMessage> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        _mappings.Add(mapping);
        return this;
    }

    public ITestEndpointMapBuilder MapGet<TResponse>(
        string route,
        Func<IServiceProvider, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IIdentifiable =>
        Map(RequestMethod.Get.ToString(), route, handler, priority);

    public ITestEndpointMapBuilder MapGet<TResponse>(
        string route,
        Func<CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IIdentifiable =>
        MapGet(route, (_, cancellationToken) => handler(cancellationToken), priority);

    public ITestEndpointMapBuilder MapPost<TRequest, TResponse>(
        string route,
        Func<IServiceProvider, TRequest, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IIdentifiable =>
        Map(RequestMethod.Post.ToString(), route, handler, priority);

    public ITestEndpointMapBuilder MapPost<TRequest, TResponse>(
        string route,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IIdentifiable =>
        MapPost<TRequest, TResponse>(route, (_, request, cancellationToken) => handler(request, cancellationToken), priority);

    public ITestEndpointMapBuilder MapPut<TRequest, TResponse>(
        string route,
        Func<IServiceProvider, TRequest, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IIdentifiable =>
        Map(RequestMethod.Put.ToString(), route, handler, priority);

    public ITestEndpointMapBuilder MapPut<TRequest, TResponse>(
        string route,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IIdentifiable =>
        MapPut<TRequest, TResponse>(route, (_, request, cancellationToken) => handler(request, cancellationToken), priority);

    public ITestEndpointMapBuilder MapDelete<TResponse>(
        string route,
        Func<IServiceProvider, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IIdentifiable =>
        Map(RequestMethod.Delete.ToString(), route, handler, priority);

    public ITestEndpointMapBuilder MapDelete<TResponse>(
        string route,
        Func<CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IIdentifiable =>
        MapDelete(route, (_, cancellationToken) => handler(cancellationToken), priority);

    public ITestEndpointMapBuilder Map<TResponse>(
        string method,
        string route,
        Func<IServiceProvider, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IIdentifiable
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentNullException.ThrowIfNull(handler);

        ApiEndpointMapping<IMessage, IMessage> mapping = UfwApiEndpointMappingFactory.Map<TResponse>(
            method,
            NormalizeRoute(route),
            priority,
            invokeAsync: (serviceProvider, _, cancellationToken) => handler(serviceProvider, cancellationToken));

        return Map(mapping);
    }

    public ITestEndpointMapBuilder Map<TRequest, TResponse>(
        string method,
        string route,
        Func<IServiceProvider, TRequest, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IIdentifiable
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentNullException.ThrowIfNull(handler);

        ApiEndpointMapping<IMessage, IMessage> mapping = UfwApiEndpointMappingFactory.Map<TRequest, TResponse>(
            method,
            NormalizeRoute(route),
            priority,
            invokeAsync: (serviceProvider, _, request, cancellationToken) => handler(serviceProvider, request, cancellationToken));

        return Map(mapping);
    }

    internal TestApiEndpointMap Build() => new(_mappings);

    private static string NormalizeRoute(string route) =>
        route.StartsWith('/') ? route : "/" + route;
}
