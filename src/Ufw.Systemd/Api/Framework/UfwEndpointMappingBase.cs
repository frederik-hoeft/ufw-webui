using Microsoft.Extensions.DependencyInjection;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Framework;

internal abstract record UfwEndpointMappingBase(string Method, string Route, int Priority) : ApiEndpointMapping<IRequestMessage, IResponseMessage>(Method, Route, Priority)
{
    protected static async ValueTask<IResponseMessage> InvokeAndSerializeAsync<TResponse>(
        IServiceProvider serviceProvider,
        Func<CancellationToken, ValueTask<TResponse>> invokeEndpointAsync,
        CancellationToken cancellationToken)
        where TResponse : IIdentifiable
    {
        IMessageSerializer messageSerializer = serviceProvider.GetRequiredService<IMessageSerializer>();
        TResponse responsePayload;
        try
        {
            responsePayload = await invokeEndpointAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            IApiExceptionMapper exceptionMapper = serviceProvider.GetRequiredService<IApiExceptionMapper>();
            return await messageSerializer.SerializeResponseAsync(exceptionMapper.Map(exception), cancellationToken);
        }
        return await messageSerializer.SerializeResponseAsync(responsePayload, cancellationToken);
    }

    protected static ValueTask<IResponseMessage> BadRequestAsync(IServiceProvider serviceProvider, string message, CancellationToken cancellationToken)
    {
        IMessageSerializer messageSerializer = serviceProvider.GetRequiredService<IMessageSerializer>();
        return messageSerializer.SerializeResponseAsync(new BadRequestResponse(message), cancellationToken);
    }
}
