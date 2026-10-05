using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Tests.Integration.Support;

internal sealed class IntegrationUfwClient : IUfwClient
{
    public InsertRuleRequest? LastInsertRequest { get; private set; }

    public ReorderRulesRequest? LastReorderRequest { get; private set; }

    public ReplaceRuleRequest? LastReplaceRequest { get; private set; }

    public RuleInsertionResponse? InsertResponse { get; set; }

    public RuleReorderResponse? ReorderResponse { get; set; }

    public RuleReplacementResponse? ReplaceResponse { get; set; }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload
        where TResponse : IEquatable<TResponse>
    {
        UfwIpcResult<TResponse> result = await TrySendAsync<TRequest, TResponse>(request, cancellationToken);
        return result.Result;
    }

    public Task<TResponse> SendAsync<TRequest, TResponse>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
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

    public Task<UfwIpcResult<TResponse>> TrySendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload
        where TResponse : IEquatable<TResponse>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is InsertRuleRequest insert
            && InsertResponse is TResponse insertResponse)
        {
            LastInsertRequest = insert;
            return Task.FromResult(UfwIpcResult<TResponse>.Success(insertResponse));
        }
        if (request is ReorderRulesRequest reorder
            && ReorderResponse is TResponse reorderResponse)
        {
            LastReorderRequest = reorder;
            return Task.FromResult(UfwIpcResult<TResponse>.Success(reorderResponse));
        }
        if (request is ReplaceRuleRequest replace
            && ReplaceResponse is TResponse replacementResponse)
        {
            LastReplaceRequest = replace;
            return Task.FromResult(UfwIpcResult<TResponse>.Success(replacementResponse));
        }

        throw new NotSupportedException($"Unsupported integration request {typeof(TRequest).Name} -> {typeof(TResponse).Name}.");
    }

    public Task<UfwIpcResult<TResponse>> TrySendAsync<TRequest, TResponse>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse> =>
        throw new NotSupportedException();

    public Task<UfwIpcResult<TResponse>> TrySendAsync<TResponse>(RequestMethod method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse> =>
        throw new NotSupportedException();

    public Task<UfwIpcResult> TrySendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload =>
        throw new NotSupportedException();

    public Task<UfwIpcResult> TrySendAsync<TRequest>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload =>
        throw new NotSupportedException();

    public Task<UfwIpcResult> TrySendAsync(RequestMethod method, string route, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
