using Ufw.Systemd.Persistence;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Security.Intent;

[TestClass]
public sealed class FileNonceStoreTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task TestTryConsumeAsync_RejectsReplayAndSurvivesReloadAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using (FileNonceStore first = new(configuration, clock, new DurableFileStore()))
            {
                Assert.IsTrue(await first.TryConsumeAsync("nonce-one", clock.GetUtcNow().ToUnixTimeSeconds() + 300, TestContext.CancellationToken));
                Assert.IsFalse(await first.TryConsumeAsync("nonce-one", clock.GetUtcNow().ToUnixTimeSeconds() + 300, TestContext.CancellationToken));
            }

            using FileNonceStore reloaded = new(configuration, clock, new DurableFileStore());
            Assert.IsFalse(await reloaded.TryConsumeAsync("nonce-one", clock.GetUtcNow().ToUnixTimeSeconds() + 300, TestContext.CancellationToken));
            Assert.IsTrue(await reloaded.TryConsumeAsync("nonce-two", clock.GetUtcNow().ToUnixTimeSeconds() + 300, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TestTryConsumeAsync_IsAtomicForConcurrentReplayAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using FileNonceStore store = new(configuration, clock, new DurableFileStore());
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
    public async Task TestTryConsumeAsync_AllowsReuseAtExpiryBoundaryAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using FileNonceStore store = new(configuration, clock, new DurableFileStore());
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
    public async Task TestTryConsumeAsync_RejectsCorruptPersistedStateAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        await File.WriteAllTextAsync(path, "# ufw-intent-nonces v1\nmalformed-record\n", TestContext.CancellationToken);
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using FileNonceStore store = new(configuration, TimeProvider.System, new DurableFileStore());
            await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
                _ = await store.TryConsumeAsync("nonce", long.MaxValue, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TestTryConsumeAsync_ThrowsWhenPersistenceFailsAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        Directory.CreateDirectory(path);
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));

        try
        {
            using FileNonceStore store = new(configuration, TimeProvider.System, new DurableFileStore());
            await Assert.ThrowsAsync<IOException>(async () =>
                _ = await store.TryConsumeAsync("nonce", long.MaxValue, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TestTryConsumeAsync_AppendFailureDoesNotPublishNonceInMemoryAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        long expiresAt = clock.GetUtcNow().ToUnixTimeSeconds() + 300;
        await File.WriteAllTextAsync(path, $"# ufw-intent-nonces v1{Environment.NewLine}seed {expiresAt}{Environment.NewLine}", TestContext.CancellationToken);
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));
        FailingAppendDurableFileStore durableFiles = new(new DurableFileStore());

        try
        {
            using (FileNonceStore store = new(configuration, clock, durableFiles))
            {
                Assert.IsFalse(await store.TryConsumeAsync("seed", expiresAt, TestContext.CancellationToken));
                durableFiles.FailNextAppend = true;
                await Assert.ThrowsAsync<IOException>(async () => _ = await store.TryConsumeAsync("retryable", expiresAt, TestContext.CancellationToken));
                Assert.IsFalse((await File.ReadAllTextAsync(path, TestContext.CancellationToken)).Contains("retryable", StringComparison.Ordinal));
                Assert.IsTrue(await store.TryConsumeAsync("retryable", expiresAt, TestContext.CancellationToken));
            }

            using FileNonceStore reloaded = new(configuration, clock, new DurableFileStore());
            Assert.IsFalse(await reloaded.TryConsumeAsync("retryable", expiresAt, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TestTryConsumeAsync_CompactsStaleRecordsAtBoundedThresholdAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "intent-nonces");
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: path));
        long stableExpiresAt = clock.GetUtcNow().ToUnixTimeSeconds() + 3600;

        try
        {
            using (FileNonceStore store = new(configuration, clock, new DurableFileStore()))
            {
                Assert.IsTrue(await store.TryConsumeAsync("stable", stableExpiresAt, TestContext.CancellationToken));
                for (int i = 0; i <= 64; i++)
                {
                    if (i > 0)
                    {
                        clock.Advance(TimeSpan.FromSeconds(1));
                    }

                    Assert.IsTrue(await store.TryConsumeAsync($"short-lived-{i}", clock.GetUtcNow().ToUnixTimeSeconds() + 1, TestContext.CancellationToken));
                }
            }

            string[] persisted = await File.ReadAllLinesAsync(path, TestContext.CancellationToken);
            CollectionAssert.AreEquivalent(new[] { "# ufw-intent-nonces v1", $"stable {stableExpiresAt}", $"short-lived-64 {clock.GetUtcNow().ToUnixTimeSeconds() + 1}" }, persisted);

            using FileNonceStore reloaded = new(configuration, clock, new DurableFileStore());
            Assert.IsFalse(await reloaded.TryConsumeAsync("stable", clock.GetUtcNow().ToUnixTimeSeconds() + 300, TestContext.CancellationToken));
            Assert.IsFalse(await reloaded.TryConsumeAsync("short-lived-64", clock.GetUtcNow().ToUnixTimeSeconds() + 300, TestContext.CancellationToken));
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

    private sealed class FailingAppendDurableFileStore(IDurableFileStore inner) : IDurableFileStore
    {
        public bool FailNextAppend { get; set; }

        public Task AppendAsync(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken)
        {
            if (FailNextAppend)
            {
                FailNextAppend = false;
                throw new IOException("Simulated append failure.");
            }

            return inner.AppendAsync(path, contents, cancellationToken);
        }

        public Task ReplaceAsync(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken) => inner.ReplaceAsync(path, contents, cancellationToken);

        public bool TryCreateNew(string path, ReadOnlyMemory<byte> contents) => inner.TryCreateNew(path, contents);

        public void Delete(string path) => inner.Delete(path);
    }
}
