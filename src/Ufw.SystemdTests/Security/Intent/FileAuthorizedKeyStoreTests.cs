using System.Security.Cryptography;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Security.Intent;

[TestClass]
public sealed class FileAuthorizedKeyStoreTests
{
    [TestMethod]
    public void TryGetKey_LoadsPemBlocksAndComputesKeyId()
    {
        using ECDsa key = IntentSigner.CreateP256();
        string directory = Path.Combine(Path.GetTempPath(), "ufw-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "authorized_keys");
        File.WriteAllText(path, "# comment\n" + key.ExportSubjectPublicKeyInfoPem() + "\n");

        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(authorizedKeysPath: path));
            using FileAuthorizedKeyStore store = new(configuration, new ConsoleLogger());
            string keyId = IntentSigner.ComputeKeyId(key);
            Assert.IsTrue(store.TryGetKey(keyId, out ECDsa? loaded));
            Assert.IsNotNull(loaded);
            Assert.IsFalse(store.TryGetKey("sha256:missing", out _));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ExtractPemBlocks_IgnoresComments()
    {
        const string file = """
            # alice
            -----BEGIN PUBLIC KEY-----
            ABC
            -----END PUBLIC KEY-----

            # bob
            -----BEGIN PUBLIC KEY-----
            DEF
            -----END PUBLIC KEY-----
            """;

        List<string> blocks = FileAuthorizedKeyStore.ExtractPemBlocks(file);
        Assert.AreEqual(2, blocks.Count);
        StringAssert.Contains(blocks[0], "ABC");
        StringAssert.Contains(blocks[1], "DEF");
    }
}
