using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Ipc.Client.Transport;

internal interface IClientMessageExchange
{
    ValueTask<IResponseMessage> ExchangeAsync(IRequestMessage request, CancellationToken cancellationToken = default);
}
