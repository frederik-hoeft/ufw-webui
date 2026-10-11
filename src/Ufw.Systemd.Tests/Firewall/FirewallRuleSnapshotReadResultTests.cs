using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class FirewallRuleSnapshotReadResultTests
{
    [TestMethod]
    public void Success_RequiresAuthoritativeSnapshot()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FirewallRuleSnapshotReadResult.Success(null!));

        FirewallSnapshot snapshot = new(Active: true, [], TestFirewallConfiguration.Enabled);
        FirewallRuleSnapshotReadResult.Success success = new(snapshot);
        Assert.AreSame(snapshot, success.Snapshot);
    }

    [TestMethod]
    public void Failure_RequiresErrorPayload()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FirewallRuleSnapshotReadResult.Failure(null!));

        InternalServerErrorResponse error = new("test failure");
        FirewallRuleSnapshotReadResult.Failure failure = new(error.Message!);
        Assert.AreEqual(error.Message, failure.Diagnostic);
    }

    [TestMethod]
    public void Success_ProjectsAuthoritativeSnapshotWithoutBranchingAtCallSites()
    {
        FirewallSnapshot snapshot = new(Active: true, [], TestFirewallConfiguration.Enabled);
        FirewallRuleSnapshotReadResult result = new FirewallRuleSnapshotReadResult.Success(snapshot);

        Assert.AreSame(snapshot, result.OrDefault());
        Assert.AreEqual(snapshot.ToResponse(), result.ToResponsePayload());
        Assert.IsTrue(result.TryGetSnapshot(out FirewallSnapshot? projected, out IResponsePayload? error));
        Assert.AreSame(snapshot, projected);
        Assert.IsNull(error);
        Assert.AreEqual("success", result.Match(static _ => "success", static _ => "failure"));
    }

    [TestMethod]
    public async Task TaskProjection_ProjectsAuthoritativeSnapshotWithoutAwaitParenthesesAsync()
    {
        FirewallSnapshot snapshot = new(Active: true, [], TestFirewallConfiguration.Enabled);
        Task<FirewallRuleSnapshotReadResult> success = Task.FromResult<FirewallRuleSnapshotReadResult>(new FirewallRuleSnapshotReadResult.Success(snapshot));
        Task<FirewallRuleSnapshotReadResult> failure = Task.FromResult<FirewallRuleSnapshotReadResult>(new FirewallRuleSnapshotReadResult.Failure("test failure"));

        Assert.AreSame(snapshot, await success.OrDefaultAsync());
        Assert.IsNull(await failure.OrDefaultAsync());
    }

    [TestMethod]
    public void Failure_ProjectsErrorWithoutBranchingAtCallSites()
    {
        InternalServerErrorResponse error = new("test failure");
        FirewallRuleSnapshotReadResult result = new FirewallRuleSnapshotReadResult.Failure(error.Message!);

        Assert.IsNull(result.OrDefault());
        Assert.AreEqual(error.Message, Assert.IsInstanceOfType<InternalServerErrorResponse>(result.ToResponsePayload()).Message);
        Assert.IsFalse(result.TryGetSnapshot(out FirewallSnapshot? snapshot, out IResponsePayload? projectedError));
        Assert.IsNull(snapshot);
        Assert.AreEqual(error.Message, Assert.IsInstanceOfType<InternalServerErrorResponse>(projectedError).Message);
        Assert.AreEqual("failure", result.Match(static _ => "success", static _ => "failure"));
    }
}
