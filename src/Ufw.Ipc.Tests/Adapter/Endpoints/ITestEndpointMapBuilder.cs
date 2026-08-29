using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Model;
using Ufw.Roslyn.Controllers.Mapping;

namespace Ufw.Ipc.Tests.Adapter.Endpoints;

/// <summary>
/// Fluent registration surface for daemon endpoints used only inside a test host.
/// Handlers are isolated from production controllers.
/// </summary>
public interface ITestEndpointMapBuilder
{
    ITestEndpointMapBuilder Map(ApiEndpointMapping<IRequestMessage, IResponseMessage> mapping);

    ITestEndpointMapBuilder MapGet<TResponse>(
        string route,
        Func<IServiceProvider, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IResponsePayload;

    ITestEndpointMapBuilder MapGet<TResponse>(
        string route,
        Func<CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IResponsePayload;

    ITestEndpointMapBuilder MapPost<TRequest, TResponse>(
        string route,
        Func<IServiceProvider, TRequest, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IResponsePayload;

    ITestEndpointMapBuilder MapPost<TRequest, TResponse>(
        string route,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IResponsePayload;

    ITestEndpointMapBuilder MapPut<TRequest, TResponse>(
        string route,
        Func<IServiceProvider, TRequest, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IResponsePayload;

    ITestEndpointMapBuilder MapPut<TRequest, TResponse>(
        string route,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IResponsePayload;

    ITestEndpointMapBuilder MapDelete<TResponse>(
        string route,
        Func<IServiceProvider, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IResponsePayload;

    ITestEndpointMapBuilder MapDelete<TResponse>(
        string route,
        Func<CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IResponsePayload;

    ITestEndpointMapBuilder Map<TResponse>(
        string method,
        string route,
        Func<IServiceProvider, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IResponsePayload;

    ITestEndpointMapBuilder Map<TRequest, TResponse>(
        string method,
        string route,
        Func<IServiceProvider, TRequest, CancellationToken, ValueTask<TResponse>> handler,
        int priority = 0)
        where TResponse : IResponsePayload;
}
