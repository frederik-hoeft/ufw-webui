using System.IO.Pipelines;

namespace Ufw.Ipc.Tests.Adapter.Transport;

/// <summary>
/// Creates a connected pair of full-duplex streams that communicate entirely in-process.
/// </summary>
internal static class DuplexStreamPair
{
    public static (Stream Client, Stream Server) Create()
    {
        Pipe clientToServer = new(new PipeOptions(useSynchronizationContext: false));
        Pipe serverToClient = new(new PipeOptions(useSynchronizationContext: false));

        DuplexPipeStream client = new(
            reader: serverToClient.Reader,
            writer: clientToServer.Writer,
            completeLocalHalf: () =>
            {
                clientToServer.Writer.Complete();
                serverToClient.Reader.Complete();
            });

        DuplexPipeStream server = new(
            reader: clientToServer.Reader,
            writer: serverToClient.Writer,
            completeLocalHalf: () =>
            {
                serverToClient.Writer.Complete();
                clientToServer.Reader.Complete();
            });

        return (client, server);
    }
}
