using System.Text;
using Ufw.Systemd.Persistence;

namespace Ufw.Systemd.Tests.Persistence;

[TestClass]
public sealed class DurableFileStoreTests
{
    [TestMethod]
    public async Task AppendAsync_CreatesParentAndAppendsContentsAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "nested", "state");
        DurableFileStore store = new();
        try
        {
            await store.AppendAsync(path, "first\n"u8.ToArray(), CancellationToken.None);
            await store.AppendAsync(path, "second\n"u8.ToArray(), CancellationToken.None);

            Assert.AreEqual("first\nsecond\n", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReplaceAsync_ReplacesTargetAndRemovesTemporaryFileAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "state");
        await File.WriteAllTextAsync(path, "old");
        DurableFileStore store = new();
        try
        {
            await store.ReplaceAsync(path, "new"u8.ToArray(), CancellationToken.None);

            Assert.AreEqual("new", await File.ReadAllTextAsync(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReplaceAsync_CancellationLeavesExistingTargetAndCleansTemporaryFileAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "state");
        await File.WriteAllTextAsync(path, "old");
        DurableFileStore store = new();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => store.ReplaceAsync(path, Encoding.UTF8.GetBytes("new"), cancellation.Token));

            Assert.AreEqual("old", await File.ReadAllTextAsync(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TryCreateNew_PreservesExistingContentsAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "nested", "state");
        DurableFileStore store = new();
        try
        {
            Assert.IsTrue(store.TryCreateNew(path, "first"u8.ToArray()));
            Assert.IsFalse(store.TryCreateNew(path, "second"u8.ToArray()));
            Assert.AreEqual("first", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Delete_RemovesTargetAndStagingFileAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "state");
        await File.WriteAllTextAsync(path, "state");
        await File.WriteAllTextAsync(path + ".tmp", "staging");
        DurableFileStore store = new();
        try
        {
            store.Delete(path);

            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-durable-file-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
