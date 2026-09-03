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
    public void TestTryGetKey_LoadsPemBlocksAndComputesKeyId()
    {
        using ECDsa key = IntentSigner.CreateP256();
        string directory = CreateTemporaryDirectory();
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
    public void TestTryGetKey_RejectsPrivateKeyPem()
    {
        using ECDsa key = IntentSigner.CreateP256();
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "authorized_keys");
        File.WriteAllText(path, key.ExportECPrivateKeyPem());

        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(authorizedKeysPath: path));
            using FileAuthorizedKeyStore store = new(configuration, new ConsoleLogger());
            Assert.ThrowsExactly<InvalidDataException>(() => store.TryGetKey(IntentSigner.ComputeKeyId(key), out _));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void TestTryGetKey_RejectsUnsupportedEcCurve()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "authorized_keys");
        File.WriteAllText(path, key.ExportSubjectPublicKeyInfoPem());

        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(authorizedKeysPath: path));
            using FileAuthorizedKeyStore store = new(configuration, new ConsoleLogger());
            Assert.ThrowsExactly<InvalidDataException>(() => store.TryGetKey(IntentSigner.ComputeKeyId(key), out _));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void TestTryGetKey_FailsClosedWhenAnyConfiguredKeyIsMalformed()
    {
        using ECDsa key = IntentSigner.CreateP256();
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "authorized_keys");
        File.WriteAllText(path, key.ExportSubjectPublicKeyInfoPem() + "\nnot-a-pem-record\n");

        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(authorizedKeysPath: path));
            using FileAuthorizedKeyStore store = new(configuration, new ConsoleLogger());
            Assert.ThrowsExactly<InvalidDataException>(() => store.TryGetKey(IntentSigner.ComputeKeyId(key), out _));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void TestExtractPemBlocks_IgnoresComments()
    {
        const string FILE = """
            # alice
            -----BEGIN PUBLIC KEY-----
            ABC
            -----END PUBLIC KEY-----

            # bob
            -----BEGIN PUBLIC KEY-----
            DEF
            -----END PUBLIC KEY-----
            """;

        List<string> blocks = FileAuthorizedKeyStore.ExtractPemBlocks(FILE);
        Assert.HasCount(2, blocks);
        Assert.Contains("ABC", blocks[0]);
        Assert.Contains("DEF", blocks[1]);
    }

    [TestMethod]
    public void TestExtractPemBlocks_RejectsUnterminatedBlock()
    {
        const string FILE = """
            -----BEGIN PUBLIC KEY-----
            ABC
            """;

        Assert.ThrowsExactly<InvalidDataException>(() => FileAuthorizedKeyStore.ExtractPemBlocks(FILE));
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
