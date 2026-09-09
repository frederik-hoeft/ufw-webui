using System.IO.Pipes;
using Ufw.Systemd.Configuration.Model;
using DaemonPipeOptions = Ufw.Systemd.Configuration.Model.PipeOptions;
using SystemPipeOptions = System.IO.Pipes.PipeOptions;
using Ufw.Systemd.Tests.TestSupport;
using Ufw.Systemd.Transport.Pipes.Unix;

namespace Ufw.Systemd.Tests.Transport;

[TestClass]
public sealed class UnixNamedPipeServerStreamDescriptorTests
{
    [TestMethod]
    public async Task ServeAsync_CreatesGroupReadableAndWritableSocketOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string directory = Path.Combine(Path.GetTempPath(), $"ufw-pipe-{Guid.NewGuid():N}");
        string pipePath = Path.Combine(directory, "ufw-systemd.sock");
        Directory.CreateDirectory(directory);
        try
        {
            TestConfiguration configuration = new(new AppSettings
            {
                Pipe = new DaemonPipeOptions { PipeName = pipePath },
                Network = new NetworkOptions(),
            });
            UnixNamedPipeServerStreamDescriptor descriptor = new(configuration);

            Task<NamedPipeServerStream> serverTask = descriptor.ServeAsync(CancellationToken.None);
            await WaitForSocketAsync(pipePath);

            UnixFileMode expected =
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupWrite;
            Assert.AreEqual(expected, File.GetUnixFileMode(pipePath));

            await using NamedPipeClientStream client = new(".", pipePath, PipeDirection.InOut, SystemPipeOptions.Asynchronous);
            await client.ConnectAsync();
            await using NamedPipeServerStream server = await serverTask;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task WaitForSocketAsync(string path)
    {
        for (int i = 0; i < 100; i++)
        {
            if (File.Exists(path))
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail($"Socket '{path}' was not created in time.");
    }
}
