using System.Security.Cryptography;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Security.Intent;

[TestClass]
public sealed class FileAuthorizedKeyStoreTests
{
    private static readonly byte[] s_payload = "signed payload"u8.ToArray();

    [TestMethod]
    public void VerifySignature_ClassifiesAuthorizedInvalidAndUnknownSignatures()
    {
        using ECDsa key = IntentSigner.CreateP256();
        using ECDsa unknownKey = IntentSigner.CreateP256();
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "authorized_keys");
        File.WriteAllText(path, "# comment\n" + key.ExportSubjectPublicKeyInfoPem() + "\n");

        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(authorizedKeysPath: path));
            FileAuthorizedKeyStore store = new(configuration, new ConsoleLogger());
            string keyId = IntentSigner.ComputeKeyId(key);

            Assert.AreEqual(AuthorizedKeyVerificationResult.Verified, store.VerifySignature(keyId, s_payload, IntentSigner.Sign(key, s_payload)));
            Assert.AreEqual(AuthorizedKeyVerificationResult.InvalidSignature, store.VerifySignature(keyId, s_payload, IntentSigner.Sign(unknownKey, s_payload)));
            Assert.AreEqual(AuthorizedKeyVerificationResult.UnknownKey, store.VerifySignature(IntentSigner.ComputeKeyId(unknownKey), s_payload, IntentSigner.Sign(unknownKey, s_payload)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Constructor_RejectsPrivateKeyPem()
    {
        using ECDsa key = IntentSigner.CreateP256();
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "authorized_keys");
        File.WriteAllText(path, key.ExportECPrivateKeyPem());

        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(authorizedKeysPath: path));
            Assert.ThrowsExactly<InvalidDataException>(() => new FileAuthorizedKeyStore(configuration, new ConsoleLogger()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Constructor_RejectsUnsupportedEcCurve()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "authorized_keys");
        File.WriteAllText(path, key.ExportSubjectPublicKeyInfoPem());

        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(authorizedKeysPath: path));
            Assert.ThrowsExactly<InvalidDataException>(() => new FileAuthorizedKeyStore(configuration, new ConsoleLogger()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Constructor_FailsClosedWhenAnyConfiguredKeyIsMalformed()
    {
        using ECDsa key = IntentSigner.CreateP256();
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "authorized_keys");
        File.WriteAllText(path, key.ExportSubjectPublicKeyInfoPem() + "\nnot-a-pem-record\n");

        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(authorizedKeysPath: path));
            Assert.ThrowsExactly<InvalidDataException>(() => new FileAuthorizedKeyStore(configuration, new ConsoleLogger()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Store_SnapshotsAuthorizedKeysUntilRestart()
    {
        using ECDsa initialKey = IntentSigner.CreateP256();
        using ECDsa replacementKey = IntentSigner.CreateP256();
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "authorized_keys");
        File.WriteAllText(path, initialKey.ExportSubjectPublicKeyInfoPem());

        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(authorizedKeysPath: path));
            FileAuthorizedKeyStore store = new(configuration, new ConsoleLogger());
            File.WriteAllText(path, replacementKey.ExportSubjectPublicKeyInfoPem());

            Assert.AreEqual(
                AuthorizedKeyVerificationResult.Verified,
                store.VerifySignature(IntentSigner.ComputeKeyId(initialKey), s_payload, IntentSigner.Sign(initialKey, s_payload)));
            Assert.AreEqual(
                AuthorizedKeyVerificationResult.UnknownKey,
                store.VerifySignature(IntentSigner.ComputeKeyId(replacementKey), s_payload, IntentSigner.Sign(replacementKey, s_payload)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void MissingKeyFile_RemainsEmptyForStoreLifetime()
    {
        using ECDsa key = IntentSigner.CreateP256();
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "authorized_keys");

        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(authorizedKeysPath: path));
            FileAuthorizedKeyStore store = new(configuration, new ConsoleLogger());
            File.WriteAllText(path, key.ExportSubjectPublicKeyInfoPem());

            Assert.AreEqual(
                AuthorizedKeyVerificationResult.UnknownKey,
                store.VerifySignature(IntentSigner.ComputeKeyId(key), s_payload, IntentSigner.Sign(key, s_payload)));
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
