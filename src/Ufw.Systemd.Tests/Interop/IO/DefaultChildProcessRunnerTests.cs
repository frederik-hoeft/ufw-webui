using System.Collections.Immutable;
using Moq;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Interop.IO;

[TestClass]
public sealed class DefaultChildProcessRunnerTests
{
    [TestMethod]
    public async Task RunAsync_DebugModeEnabled_LogsCommandAndCapturedStreams()
    {
        (DefaultChildProcessRunner runner, Mock<ILogger<DefaultChildProcessRunner>> logger) = CreateRunner(debugMode: true);
        ChildProcessRequest request = CreateRequest();

        ChildProcessResult result = await runner.RunAsync(request, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("stdout", result.StandardOutput);
        Assert.AreEqual("stderr", result.StandardError);
        Assert.IsFalse(result.CancellationRequested);
        logger.Verify(
            value => value.LogDebug(It.Is<string>(message => message.Contains("Executing child process:", StringComparison.Ordinal) && message.Contains("/bin/sh", StringComparison.Ordinal))),
            Times.Once);
        logger.Verify(value => value.LogDebug(It.Is<string>(message => message == "Child process standard output: stdout")), Times.Once);
        logger.Verify(value => value.LogDebug(It.Is<string>(message => message == "Child process standard error: stderr")), Times.Once);
    }

    [TestMethod]
    public async Task RunAsync_DebugModeDisabled_CapturesStreamsWithoutLoggingDiagnostics()
    {
        (DefaultChildProcessRunner runner, Mock<ILogger<DefaultChildProcessRunner>> logger) = CreateRunner(debugMode: false);

        ChildProcessResult result = await runner.RunAsync(CreateRequest(), CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("stdout", result.StandardOutput);
        Assert.AreEqual("stderr", result.StandardError);
        Assert.IsFalse(result.CancellationRequested);
        logger.Verify(value => value.LogDebug(It.IsAny<string>()), Times.Never);
    }

    private static (DefaultChildProcessRunner Runner, Mock<ILogger<DefaultChildProcessRunner>> Logger) CreateRunner(bool debugMode)
    {
        Mock<ILogger<DefaultChildProcessRunner>> scopedLogger = new();
        Mock<ILogger> logger = new();
        logger.Setup(value => value.Scoped<DefaultChildProcessRunner>()).Returns(scopedLogger.Object);
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(debugMode: debugMode));
        return (new DefaultChildProcessRunner(configuration, logger.Object), scopedLogger);
    }

    private static ChildProcessRequest CreateRequest() =>
        new("/bin/sh", ImmutableArray.Create("-c", "printf 'stdout'; printf 'stderr' >&2"), ImmutableDictionary<string, string>.Empty);
}
