using System.Collections.Immutable;
using Ufw.Systemd.Interop.IO;

namespace Ufw.Systemd.Tests.Interop.IO;

[TestClass]
public sealed class UfwProcessResultTests
{
    [TestMethod]
    [DataRow(0, false, true)]
    [DataRow(0, true, false)]
    [DataRow(1, false, false)]
    [DataRow(1, true, false)]
    public void Succeeded_RequiresZeroExitCodeAndCompletedExecution(int exitCode, bool cancellationRequested, bool expected)
    {
        UfwProcessResult result = new(exitCode, string.Empty, string.Empty, [], cancellationRequested);

        Assert.AreEqual(expected, result.Succeeded);
    }
}
