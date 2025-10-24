using System.Security.Authentication;

namespace Ufw.Pipes.Client.Configuration;

internal sealed record UfwClientOptions(string ServerName, string PipeName, SslProtocols SslProtocols);
