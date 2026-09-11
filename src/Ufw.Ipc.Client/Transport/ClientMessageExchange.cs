using Ufw.Ipc.Client.Configuration;
using Ufw.Shared.Ipc.Protocol;
using Ufw.Shared.Ipc.Serialization;
using Ufw.Shared.Ipc.Transport;
using Ufw.Shared.Ipc.Transport.Itp;
using Ufw.Shared.Ipc.Transport.Security;

namespace Ufw.Ipc.Client.Transport;

internal sealed class ClientMessageExchange(
    IMessageSerializer messageSerializer,
    ITransportLayerService transportLayerService,
    ITransportSecurityService transportSecurityService,
    UfwClientOptions options,
    ItpOptions itpOptions) : IClientMessageExchange
{
    public async ValueTask<IResponseMessage> ExchangeAsync(IRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using ITransportLayerConnection connection = await transportLayerService.ConnectAsync(cancellationToken);
        await using Stream stream = connection.GetStream(options.IoTimeout, options.IoTimeout);
        await using Stream secureStream = await transportSecurityService.OpenSecureStreamAsync(stream, cancellationToken);

        ItpConnection itp = new(secureStream, itpOptions);
        await itp.WriteApplicationDataAsync(messageSerializer.Encode(request), cancellationToken);

        ItpFrame frame = await itp.ReadAsync(cancellationToken);
        IMessage decoded = messageSerializer.Decode(frame.Payload);
        if (decoded is IResponseMessage response)
        {
            return response;
        }

        await decoded.DisposeAsync();
        throw new ApplicationProtocolException(ApplicationProtocolError.InvalidKind, "Peer returned an application document that is not a response.");
    }
}
