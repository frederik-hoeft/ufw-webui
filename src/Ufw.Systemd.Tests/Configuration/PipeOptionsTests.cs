using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.Configuration;

[TestClass]
public sealed class PipeOptionsTests
{
    [TestMethod]
    public void ThrowIfInvalid_AcceptsNonEmptyEndpoint()
    {
        PipeOptions options = new() { PipeName = "/tmp/ufw-tests.pipe" };

        options.ThrowIfInvalid();
    }

    [TestMethod]
    public void ThrowIfInvalid_RejectsEmptyEndpoint()
    {
        PipeOptions options = new() { PipeName = "" };

        Assert.ThrowsExactly<InvalidOperationException>(options.ThrowIfInvalid);
    }

    [TestMethod]
    public void ThrowIfInvalid_DoesNotApplyHostSpecificPathRules()
    {
        PipeOptions options = new() { PipeName = "relative-pipe-name" };

        options.ThrowIfInvalid();
    }
}
