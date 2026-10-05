using Moq;
using System.Collections.Immutable;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class UfwProcessExecutorTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ExecuteAsync_SuccessReturnsCleanResultAsync()
    {
        Mock<IUfwRunner> runner = new(MockBehavior.Strict);
        runner.Setup(value => value.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessResult(exitCode: 0));
        UfwProcessExecutor executor = new(runner.Object, new ConsoleLogger());

        UfwProcessExecutionResult result = await executor.ExecuteAsync(new UfwDeleteRuleCommand(1), "deleting a test rule", TestContext.CancellationToken);

        Assert.IsTrue(result.Succeeded);
        Assert.IsFalse(result.CancellationRequested);
        Assert.IsFalse(result.RunnerFailed);
        Assert.IsNull(result.Diagnostic);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProcessFailureFormatsOneStandardDiagnosticAsync()
    {
        Mock<IUfwRunner> runner = new(MockBehavior.Strict);
        runner.Setup(value => value.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessResult(exitCode: 2, standardError: "permission denied"));
        UfwProcessExecutor executor = new(runner.Object, new ConsoleLogger());

        UfwProcessExecutionResult result = await executor.ExecuteAsync(new UfwDeleteRuleCommand(1), "deleting a test rule", TestContext.CancellationToken);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(result.RunnerFailed);
        StringAssert.Contains(result.Diagnostic, "exited with code 2");
        StringAssert.Contains(result.Diagnostic, "deleting a test rule");
        StringAssert.Contains(result.Diagnostic, "permission denied");
    }

    [TestMethod]
    public async Task ExecuteAsync_PostStartCancellationPreservesCancellationStateAsync()
    {
        Mock<IUfwRunner> runner = new(MockBehavior.Strict);
        runner.Setup(value => value.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessResult(exitCode: 137, cancellationRequested: true));
        UfwProcessExecutor executor = new(runner.Object, new ConsoleLogger());

        UfwProcessExecutionResult result = await executor.ExecuteAsync(new UfwDeleteRuleCommand(1), "deleting a test rule", TestContext.CancellationToken);

        Assert.IsFalse(result.Succeeded);
        Assert.IsTrue(result.CancellationRequested);
        Assert.IsFalse(result.RunnerFailed);
        Assert.AreEqual("UFW process was canceled after it started while deleting a test rule.", result.Diagnostic);
    }

    [TestMethod]
    public async Task ExecuteAsync_ChildProcessFailureBecomesRunnerFailureAsync()
    {
        Mock<IUfwRunner> runner = new(MockBehavior.Strict);
        runner.Setup(value => value.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ChildProcessException("wait failed", new IOException("test")));
        UfwProcessExecutor executor = new(runner.Object, new ConsoleLogger());

        UfwProcessExecutionResult result = await executor.ExecuteAsync(new UfwDeleteRuleCommand(1), "deleting a test rule", TestContext.CancellationToken);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(result.CancellationRequested);
        Assert.IsTrue(result.RunnerFailed);
        Assert.AreEqual("wait failed", result.Diagnostic);
    }

    [TestMethod]
    public void CombineDiagnostics_IgnoresEmptyValuesAndPreservesOrder()
    {
        Assert.AreEqual("first second", UfwProcessDiagnostics.Combine(null, " ", "first", null, "second"));
        Assert.IsNull(UfwProcessDiagnostics.Combine(null, string.Empty, " "));
    }

    private static UfwProcessResult ProcessResult(int exitCode, string standardError = "", bool cancellationRequested = false) =>
        new(exitCode, string.Empty, standardError, ["delete", "1"], cancellationRequested);
}
