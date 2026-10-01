using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.Configuration;

[TestClass]
public sealed class SecurityOptionsTests
{
    [TestMethod]
    public void ThrowIfInvalid_DoesNotConsultFilesystem()
    {
        string directory = Path.GetTempPath();
        SecurityOptions options = CreateOptions(directory);

        options.ThrowIfInvalid();
    }

    [TestMethod]
    public void ThrowIfInvalid_RejectsInvalidTimingPolicy()
    {
        SecurityOptions invalidAge = CreateOptions("/tmp/authorized-keys", maxIntentAge: TimeSpan.Zero);
        SecurityOptions invalidSkew = CreateOptions("/tmp/authorized-keys", clockSkew: TimeSpan.FromTicks(-1));

        Assert.ThrowsExactly<InvalidOperationException>(invalidAge.ThrowIfInvalid);
        Assert.ThrowsExactly<InvalidOperationException>(invalidSkew.ThrowIfInvalid);
    }

    private static SecurityOptions CreateOptions(
        string authorizedKeysPath,
        TimeSpan? maxIntentAge = null,
        TimeSpan? clockSkew = null) =>
        new()
        {
            AuthorizedKeysPath = authorizedKeysPath,
            NonceStorePath = "/tmp/nonces",
            DeploymentIdPath = "/tmp/deployment-id",
            ReorderRecoveryJournalPath = "/tmp/reorder-recovery.json",
            MaxIntentAge = maxIntentAge ?? TimeSpan.FromMinutes(5),
            ClockSkew = clockSkew ?? TimeSpan.FromSeconds(30),
        };
}
