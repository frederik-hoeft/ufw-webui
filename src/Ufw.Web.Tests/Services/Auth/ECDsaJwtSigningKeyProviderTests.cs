using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Ufw.Web.Configuration;
using Ufw.Web.Services.Auth;

namespace Ufw.Web.Tests.Services.Auth;

[TestClass]
public sealed class ECDsaJwtSigningKeyProviderTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_P256Pkcs8Key_ExposesUsableEs256Key()
    {
        string keyPath = Path.Combine(TestContext.TestRunDirectory!, $"jwt-{Guid.NewGuid():N}.pem");
        using ECDsa sourceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(keyPath, sourceKey.ExportPkcs8PrivateKeyPem());

        try
        {
            JwtOptions options = new() { SigningKeyPath = keyPath };
            using ECDsaJwtSigningKeyProvider provider = new(Options.Create(options));

            Assert.AreEqual(SecurityAlgorithms.EcdsaSha256, provider.SigningAlgorithm);
            Assert.IsInstanceOfType<ECDsaSecurityKey>(provider.SigningKey);

            ECDsaSecurityKey signingKey = (ECDsaSecurityKey)provider.SigningKey;
            Assert.AreEqual(256, signingKey.ECDsa.KeySize);

            byte[] payload = [1, 2, 3, 4];
            byte[] signature = signingKey.ECDsa.SignData(payload, HashAlgorithmName.SHA256);
            Assert.IsTrue(sourceKey.VerifyData(payload, signature, HashAlgorithmName.SHA256));
        }
        finally
        {
            File.Delete(keyPath);
        }
    }
}
