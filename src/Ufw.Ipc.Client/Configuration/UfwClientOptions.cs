using System.Security.Authentication;

namespace Ufw.Ipc.Client.Configuration;

internal sealed record UfwClientOptions(string ServerName, string PipeName, SslProtocols SslProtocols);
