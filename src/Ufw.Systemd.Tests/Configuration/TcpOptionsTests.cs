using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.Configuration;

[TestClass]
public sealed class TcpOptionsTests
{
    [TestMethod]
    public void ThrowIfInvalid_AcceptsIpv4AndIpv6ListenAddresses()
    {
        new TcpOptions { ListenAddress = "127.0.0.1", Port = 1234 }.ThrowIfInvalid();
        new TcpOptions { ListenAddress = "::1", Port = 1234 }.ThrowIfInvalid();
    }

    [TestMethod]
    [DataRow("localhost", 1234)]
    [DataRow("", 1234)]
    [DataRow("127.0.0.1", 0)]
    [DataRow("127.0.0.1", 65536)]
    public void ThrowIfInvalid_RejectsInvalidEndpoint(string listenAddress, int port)
    {
        TcpOptions options = new() { ListenAddress = listenAddress, Port = port };

        Assert.ThrowsExactly<InvalidOperationException>(options.ThrowIfInvalid);
    }
}
