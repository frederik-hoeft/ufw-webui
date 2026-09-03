using Moq;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Interop.IO;

[TestClass]
public sealed class UfwRunnerTests
{
    private static readonly string[] s_statusArguments = ["status", "numbered"];

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ExecuteAsync_UsesDeterministicLocaleAndKeepsOutputStreamsSeparateAsync()
    {
        TestConfiguration configuration = new(TestAppSettingsFactory.Create());
        Mock<IChildProcessRunner> processRunner = new();
        processRunner
            .Setup(static runner => runner.RunAsync(It.IsAny<ChildProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChildProcessResult(
                0,
                UfwStatusFixtures.EMPTY_ACTIVE,
                "diagnostic stderr\n",
                CancellationRequested: false));
        UfwRunner runner = new(configuration, processRunner.Object);
        UfwListCommand command = new();

        UfwProcessResult result = await runner.ExecuteAsync(command, TestContext.CancellationToken);
        UfwStatusSnapshot? snapshot = await command.GetResultAsync(TestContext.CancellationToken);

        Assert.IsNotNull(snapshot);
        Assert.IsTrue(snapshot.Active);
        Assert.AreEqual(UfwStatusFixtures.EMPTY_ACTIVE, result.StandardOutput);
        Assert.AreEqual("diagnostic stderr\n", result.StandardError);
        processRunner.Verify(
            static runner => runner.RunAsync(
                It.Is<ChildProcessRequest>(request =>
                    request.Command == "/usr/sbin/ufw"
                    && request.Arguments.SequenceEqual(s_statusArguments)
                    && request.Environment["LC_ALL"] == "C"
                    && request.Environment["LANG"] == "C"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_PropagatesPostStartCancellationStateAsync()
    {
        TestConfiguration configuration = new(TestAppSettingsFactory.Create());
        Mock<IChildProcessRunner> processRunner = new();
        processRunner
            .Setup(static runner => runner.RunAsync(It.IsAny<ChildProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChildProcessResult(137, string.Empty, "terminated\n", CancellationRequested: true));
        UfwRunner runner = new(configuration, processRunner.Object);

        UfwProcessResult result = await runner.ExecuteAsync(new UfwListCommand(), TestContext.CancellationToken);

        Assert.IsTrue(result.CancellationRequested);
        Assert.AreEqual(137, result.ExitCode);
    }
}
