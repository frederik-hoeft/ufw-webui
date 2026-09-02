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
        string directory = CreateTemporaryDirectory();
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
    public async Task TryConsumeAsync_IsAtomicForConcurrentReplayAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using FileNonceStore store = new(configuration, clock);
            long expiresAt = clock.GetUtcNow().ToUnixTimeSeconds() + 300;
            bool[] results = await Task.WhenAll(
                store.TryConsumeAsync("same-nonce", expiresAt, TestContext.CancellationToken).AsTask(),
                store.TryConsumeAsync("same-nonce", expiresAt, TestContext.CancellationToken).AsTask());

            Assert.AreEqual(1, results.Count(static result => result));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TryConsumeAsync_AllowsReuseAtExpiryBoundaryAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using FileNonceStore store = new(configuration, clock);
            long expiresAt = clock.GetUtcNow().ToUnixTimeSeconds() + 30;
            Assert.IsTrue(await store.TryConsumeAsync("old-nonce", expiresAt, TestContext.CancellationToken));
            clock.Advance(TimeSpan.FromSeconds(30));
            Assert.IsTrue(await store.TryConsumeAsync("old-nonce", clock.GetUtcNow().ToUnixTimeSeconds() + 30, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TryConsumeAsync_RejectsCorruptPersistedStateAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        File.WriteAllText(path, "# ufw-intent-nonces v1\nmalformed-record\n");
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using FileNonceStore store = new(configuration, TimeProvider.System);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
                _ = await store.TryConsumeAsync("nonce", long.MaxValue, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TryConsumeAsync_ThrowsWhenPersistenceFailsAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        Directory.CreateDirectory(path);
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using FileNonceStore store = new(configuration, TimeProvider.System);
            await Assert.ThrowsAsync<IOException>(async () =>
                _ = await store.TryConsumeAsync("nonce", long.MaxValue, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-nonce-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
