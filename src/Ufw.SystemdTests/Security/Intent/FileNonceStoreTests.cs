using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Security.Intent;

[TestClass]
public sealed class FileNonceStoreTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task TryConsumeAsync_RejectsReplayAndSurvivesReloadAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-nonce-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "intent-nonces");
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using (FileNonceStore first = new(configuration, clock))
            {
                Assert.IsTrue(await first.TryConsumeAsync("nonce-one", clock.GetUtcNow().ToUnixTimeSeconds() + 300, TestContext.CancellationToken));
                Assert.IsFalse(await first.TryConsumeAsync("nonce-one", clock.GetUtcNow().ToUnixTimeSeconds() + 300, TestContext.CancellationToken));
            }

            using FileNonceStore reloaded = new(configuration, clock);
            Assert.IsFalse(await reloaded.TryConsumeAsync("nonce-one", clock.GetUtcNow().ToUnixTimeSeconds() + 300, TestContext.CancellationToken));
            Assert.IsTrue(await reloaded.TryConsumeAsync("nonce-two", clock.GetUtcNow().ToUnixTimeSeconds() + 300, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TryConsumeAsync_AllowsReuseAfterExpiryAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-nonce-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "intent-nonces");
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using FileNonceStore store = new(configuration, clock);
            long expiresAt = clock.GetUtcNow().ToUnixTimeSeconds() + 30;
            Assert.IsTrue(await store.TryConsumeAsync("old-nonce", expiresAt, TestContext.CancellationToken));
            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.IsTrue(await store.TryConsumeAsync("old-nonce", clock.GetUtcNow().ToUnixTimeSeconds() + 30, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
