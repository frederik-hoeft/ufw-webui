using System.Security.Cryptography;
using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Security.Intent;

[TestClass]
public sealed class IntentVerificationPipelineTests
{
    private const string DEPLOYMENT_ID = "deployment-a";

    [TestMethod]
    public void EnvelopeVerifier_RejectsInvalidEnvelopeBeforeBindingPayload()
    {
        TestConfiguration configuration = new(TestAppSettingsFactory.Create());
        Mock<IAuthorizedKeyStore> keys = new(MockBehavior.Strict);
        Mock<IIntentPayloadBinder<AddRulePayload>> binder = new(MockBehavior.Strict);
        IntentEnvelopeVerifier verifier = new(keys.Object, new StaticDeploymentIdentityProvider(DEPLOYMENT_ID), configuration, TimeProvider.System);
        AddRuleRequest request = CreateRequest() with { Operation = IntentOperations.DELETE_RULE };

        IntentVerificationResult result = verifier.Verify(
            request,
            IntentOperations.ADD_RULE,
            binder.Object,
            static (verified, payload) => new IntentVerificationResult.AcceptedRuleMutation(verified.KeyId, verified.Nonce, verified.ExpiresAtUnix, payload.Rule, null));

        Assert.IsInstanceOfType<BadRequestResponse>(Assert.IsInstanceOfType<IntentVerificationResult.Rejected>(result).Response);
        binder.VerifyNoOtherCalls();
        keys.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void EnvelopeVerifier_RejectsBoundPayloadBeforeLookingUpSigningKey()
    {
        TestConfiguration configuration = new(TestAppSettingsFactory.Create());
        Mock<IAuthorizedKeyStore> keys = new(MockBehavior.Strict);
        Mock<IIntentPayloadBinder<AddRulePayload>> binder = new(MockBehavior.Strict);
        AddRuleRequest request = CreateRequest();
        binder.Setup(static item => item.Bind(It.IsAny<ISignedIntent>()))
            .Returns(new IntentPayloadBindingResult<AddRulePayload>.Rejected(new BadRequestResponse("invalid payload")));
        IntentEnvelopeVerifier verifier = new(keys.Object, new StaticDeploymentIdentityProvider(DEPLOYMENT_ID), configuration, TimeProvider.System);

        IntentVerificationResult result = verifier.Verify(
            request,
            IntentOperations.ADD_RULE,
            binder.Object,
            static (verified, payload) => new IntentVerificationResult.AcceptedRuleMutation(verified.KeyId, verified.Nonce, verified.ExpiresAtUnix, payload.Rule, null));

        Assert.IsInstanceOfType<BadRequestResponse>(Assert.IsInstanceOfType<IntentVerificationResult.Rejected>(result).Response);
        binder.Verify(item => item.Bind(request), Times.Once);
        keys.VerifyNoOtherCalls();
    }


    [TestMethod]
    [DataRow((int)AuthorizedKeyVerificationResult.UnknownKey, "Intent was not signed by an authorized key.")]
    [DataRow((int)AuthorizedKeyVerificationResult.InvalidSignature, "Intent signature is invalid.")]
    public void EnvelopeVerifier_MapsKeyVerificationFailuresToDistinctForbiddenResponses(int verificationResultValue, string expectedMessage)
    {
        TestConfiguration configuration = new(TestAppSettingsFactory.Create());
        Mock<IAuthorizedKeyStore> keys = new(MockBehavior.Strict);
        Mock<IIntentPayloadBinder<AddRulePayload>> binder = new(MockBehavior.Strict);
        AddRuleRequest request = CreateRequest();
        AddRulePayload payload = new() { Rule = CreateRule() };
        binder.Setup(static item => item.Bind(It.IsAny<ISignedIntent>())).Returns(new IntentPayloadBindingResult<AddRulePayload>.Accepted(payload, [1, 2, 3]));
        AuthorizedKeyVerificationResult verificationResult = (AuthorizedKeyVerificationResult)verificationResultValue;
        keys.Setup(store => store.VerifySignature(request.KeyId, It.IsAny<ReadOnlyMemory<byte>>(), request.Signature)).Returns(verificationResult);
        IntentEnvelopeVerifier verifier = new(keys.Object, new StaticDeploymentIdentityProvider(DEPLOYMENT_ID), configuration, TimeProvider.System);

        IntentVerificationResult result = verifier.Verify(
            request,
            IntentOperations.ADD_RULE,
            binder.Object,
            static (verified, boundPayload) => new IntentVerificationResult.AcceptedRuleMutation(verified.KeyId, verified.Nonce, verified.ExpiresAtUnix, boundPayload.Rule, null));

        ForbiddenResponse response = Assert.IsInstanceOfType<ForbiddenResponse>(Assert.IsInstanceOfType<IntentVerificationResult.Rejected>(result).Response);
        Assert.AreEqual(expectedMessage, response.Message);
    }

    [TestMethod]
    public void PayloadBindingResult_RequiresVariantPayloads()
    {
        AddRulePayload payload = new() { Rule = CreateRule() };

        Assert.Throws<ArgumentNullException>(() => new IntentPayloadBindingResult<AddRulePayload>.Accepted(null!, []));
        Assert.Throws<ArgumentNullException>(() => new IntentPayloadBindingResult<AddRulePayload>.Accepted(payload, null!));
        Assert.Throws<ArgumentNullException>(() => new IntentPayloadBindingResult<AddRulePayload>.Rejected(null!));
    }

    [TestMethod]
    public void AddPayloadBinder_NormalizesPayloadAndCanonicalizesNormalizedValues()
    {
        using ECDsa key = IntentSigner.CreateP256();
        AddRulePayload payload = new()
        {
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                Direction = FirewallDirection.In,
                Protocol = FirewallProtocol.Tcp,
                Source = "Anywhere",
                DestinationPorts = "80,22",
            }
        };
        AddRuleRequest request = IntentRequestFactory.CreateAddRequest(
            key,
            DEPLOYMENT_ID,
            payload,
            MessageJsonSerializerContext.Default.AddRulePayload,
            new TestTimeProvider(DateTimeOffset.Parse("2026-04-01T12:00:00Z")));
        AddIntentPayloadBinder binder = new(MessageJsonSerializerContext.Default);

        IntentPayloadBindingResult<AddRulePayload>.Accepted accepted =
            Assert.IsInstanceOfType<IntentPayloadBindingResult<AddRulePayload>.Accepted>(binder.Bind(request));

        Assert.AreEqual("any", accepted.Payload.Rule.Source);
        Assert.AreEqual("22,80", accepted.Payload.Rule.DestinationPorts);
        CollectionAssert.AreEqual(IntentCanonicalizer.CanonicalizeAdd(request, accepted.Payload), accepted.Canonical);
    }

    private static AddRuleRequest CreateRequest() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = DEPLOYMENT_ID,
        KeyId = "sha256:test",
        Nonce = IntentSigner.CreateNonce(),
        IssuedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Operation = IntentOperations.ADD_RULE,
        Payload = System.Text.Json.JsonSerializer.SerializeToElement(new AddRulePayload { Rule = CreateRule() }, MessageJsonSerializerContext.Default.AddRulePayload),
        Signature = "AA",
    };

    private static FirewallRuleSpecification CreateRule() => new()
    {
        Action = FirewallAction.Allow,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = "22",
    };

    private sealed class StaticDeploymentIdentityProvider(string deploymentId) : IDeploymentIdentityProvider
    {
        public string GetDeploymentId() => deploymentId;
    }
}
