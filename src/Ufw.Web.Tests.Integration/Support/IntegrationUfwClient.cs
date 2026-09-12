using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Tests.Integration.Support;

internal sealed class IntegrationUfwClient : IUfwClient
{
    public ReorderRulesRequest? LastReorderRequest { get; private set; }

    public RuleReorderResponse? ReorderResponse { get; set; }

    public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload
        where TResponse : IEquatable<TResponse>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is ReorderRulesRequest reorder
            && ReorderResponse is TResponse response)
        {
            LastReorderRequest = reorder;
            return Task.FromResult(response);
        }

        throw new NotSupportedException($"Unsupported integration request {typeof(TRequest).Name} -> {typeof(TResponse).Name}.");
    }

    public Task<TResponse> SendAsync<TRequest, TResponse>(
        RequestMethod method,
        string route,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse> =>
        throw new NotSupportedException();

    public Task<TResponse> SendAsync<TResponse>(RequestMethod method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse> =>
        throw new NotSupportedException();

    public Task SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload =>
        throw new NotSupportedException();

    public Task SendAsync<TRequest>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload =>
        throw new NotSupportedException();

    public Task SendAsync(RequestMethod method, string route, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
