using Moq;
using System.Text;
using Ufw.Client.Intent;
using Ufw.Client.Tests.Support;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Tests.Intent;

[TestClass]
public sealed class BrowserIntentSigningServiceTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 11, 16, 30, 45, TimeSpan.Zero);

    [TestMethod]
    public async Task CreateAddRuleRequestAsync_SignsExactCanonicalNormalizedIntentAsync()
    {
        Mock<IBrowserIntentCryptoService> crypto = CreateCrypto(out CapturedSignature captured);
        BrowserIntentSigningService service = new(crypto.Object, new MutableTimeProvider(s_now));
        FirewallRuleSpecification rule = CreateNonCanonicalRule();

        AddRuleRequest request = await service.CreateAddRuleRequestAsync("deployment", rule, "private-key");

        Assert.AreEqual("deployment", request.DeploymentId);
        Assert.AreEqual("key-id", request.KeyId);
        Assert.AreEqual("nonce", request.Nonce);
        Assert.AreEqual(IntentOperations.ADD_RULE, request.Operation);
        Assert.AreEqual(s_now.ToUnixTimeSeconds(), request.IssuedAtUnix);
        Assert.AreEqual("signature", request.Signature);
        CollectionAssert.AreEqual(IntentCanonicalizer.Canonicalize(request, rule), captured.Payload);
        Assert.AreEqual("private-key", captured.PrivateKey);
        Assert.AreEqual("any", request.Payload.GetProperty("rule").GetProperty("source").GetString());
        Assert.AreEqual("443", request.Payload.GetProperty("rule").GetProperty("destinationPorts").GetString());
        crypto.Verify(service => service.CreateNonceAsync(IntentProtocol.NONCE_SIZE_BYTES, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CreateDeleteRuleRequestAsync_BindsStableRuleIdIntoSignedPayloadAsync()
    {
        Mock<IBrowserIntentCryptoService> crypto = CreateCrypto(out CapturedSignature captured);
        BrowserIntentSigningService service = new(crypto.Object, new MutableTimeProvider(s_now));
        FirewallRuleSpecification rule = CreateNonCanonicalRule();

        DeleteRuleRequest request = await service.CreateDeleteRuleRequestAsync("deployment", "stable-id", rule, "private-key");

        Assert.AreEqual(IntentOperations.DELETE_RULE, request.Operation);
        Assert.AreEqual("stable-id", request.Payload.GetProperty("ruleId").GetString());
        CollectionAssert.AreEqual(IntentCanonicalizer.Canonicalize(request, rule, "stable-id"), captured.Payload);
    }

    [TestMethod]
    public async Task CreateRequests_InvalidInputsFailBeforeCryptoAsync()
    {
        Mock<IBrowserIntentCryptoService> crypto = new();
        BrowserIntentSigningService service = new(crypto.Object, TimeProvider.System);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.CreateAddRuleRequestAsync(" ", new FirewallRuleSpecification(), "key"));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.CreateAddRuleRequestAsync("deployment", new FirewallRuleSpecification(), " "));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.CreateDeleteRuleRequestAsync("deployment", " ", new FirewallRuleSpecification(), "key"));
        crypto.VerifyNoOtherCalls();
    }

    private static Mock<IBrowserIntentCryptoService> CreateCrypto(out CapturedSignature captured)
    {
        CapturedSignature state = new();
        Mock<IBrowserIntentCryptoService> crypto = new();
        crypto.Setup(service => service.GetKeyIdAsync("private-key", It.IsAny<CancellationToken>())).ReturnsAsync("key-id");
        crypto.Setup(service => service.CreateNonceAsync(IntentProtocol.NONCE_SIZE_BYTES, It.IsAny<CancellationToken>())).ReturnsAsync("nonce");
        crypto.Setup(service => service.SignAsync("private-key", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback((string key, byte[] payload, CancellationToken _) =>
            {
                state.PrivateKey = key;
                state.Payload = payload;
            })
            .ReturnsAsync("signature");
        captured = state;
        return crypto;
    }

    private static FirewallRuleSpecification CreateNonCanonicalRule() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = "  any  ",
        Destination = "10.0.0.1",
        DestinationPorts = "443",
        Comment = "  admin  ",
    };

    private sealed class CapturedSignature
    {
        public string? PrivateKey { get; set; }

        public byte[] Payload { get; set; } = [];
    }
}
