using System.Security.Cryptography;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Ipc.Shared.Serialization.Json;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Security.Intent;

[TestClass]
public sealed class IntentSignatureTests
{
    private const string DEPLOYMENT_ID = "deployment-a";

    [TestMethod]
    public void VerifyAdd_AcceptsFreshSignatureFromAuthorizedKey()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(key, clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        Assert.IsInstanceOfType<IntentVerificationResult.Accepted>(verifier.VerifyAdd(request));
    }

    [TestMethod]
    public void VerifyAdd_RejectsTamperedPayload()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(key, clock) with
        {
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(
                new AddRulePayload
                {
                    Rule = new FirewallRuleSpecification
                    {
                        Action = FirewallAction.Deny,
                        Direction = FirewallDirection.In,
                        Protocol = FirewallProtocol.Tcp,
                        DestinationPorts = "22",
                    }
                },
                MessageJsonSerializerContext.Default.AddRulePayload)
        };
        IntentVerifier verifier = CreateVerifier(key, clock);

        AssertRejected<ForbiddenResponse>(verifier.VerifyAdd(request));
    }

    [TestMethod]
    public void VerifyAdd_RejectsMalformedPayload()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(key, clock) with
        {
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { rule = "invalid" })
        };
        IntentVerifier verifier = CreateVerifier(key, clock);

        AssertRejected<BadRequestResponse>(verifier.VerifyAdd(request));
    }

    [TestMethod]
    public void VerifyAdd_RejectsUnknownKey()
    {
        using ECDsa signer = IntentSigner.CreateP256();
        using ECDsa authorized = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(signer, clock);
        IntentVerifier verifier = CreateVerifier(authorized, clock);

        AssertRejected<ForbiddenResponse>(verifier.VerifyAdd(request));
    }

    [TestMethod]
    public void VerifyAdd_RejectsWrongDeployment()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(key, clock, deploymentId: "deployment-b");
        IntentVerifier verifier = CreateVerifier(key, clock);

        AssertRejected<ForbiddenResponse>(verifier.VerifyAdd(request));
    }

    [TestMethod]
    public void VerifyAdd_RejectsTamperedDeploymentIdEvenWhenItMatchesVerifierDeployment()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(key, clock) with { DeploymentId = "deployment-b" };
        IntentVerifier verifier = CreateVerifier(key, clock, deploymentId: "deployment-b");

        AssertRejected<ForbiddenResponse>(verifier.VerifyAdd(request));
    }

    [TestMethod]
    public void VerifyAdd_RejectsOperationSubstitution()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(key, clock) with { Operation = IntentOperations.DELETE_RULE };
        IntentVerifier verifier = CreateVerifier(key, clock);

        AssertRejected<BadRequestResponse>(verifier.VerifyAdd(request));
    }

    [TestMethod]
    public void VerifyAdd_ExpiresAtExactReplayBoundary()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(key, clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(29));
        Assert.IsInstanceOfType<IntentVerificationResult.Accepted>(verifier.VerifyAdd(request));

        clock.Advance(TimeSpan.FromSeconds(1));
        AssertRejected<ForbiddenResponse>(verifier.VerifyAdd(request));
    }

    [TestMethod]
    public void VerifyAdd_AcceptsAtFutureSkewBoundaryAndRejectsBeyondIt()
    {
        using ECDsa key = IntentSigner.CreateP256();
        DateTimeOffset now = DateTimeOffset.Parse("2026-04-01T12:00:00Z");
        TestTimeProvider signerClock = new(now + TimeSpan.FromSeconds(30));
        AddRuleRequest boundary = SignAdd(key, signerClock);
        IntentVerifier verifier = CreateVerifier(key, new TestTimeProvider(now));

        Assert.IsInstanceOfType<IntentVerificationResult.Accepted>(verifier.VerifyAdd(boundary));

        signerClock.Advance(TimeSpan.FromSeconds(1));
        AddRuleRequest beyond = SignAdd(key, signerClock);
        AssertRejected<ForbiddenResponse>(verifier.VerifyAdd(beyond));
    }

    [TestMethod]
    public void VerifyDelete_RejectsMismatchedRuleId()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        FirewallRuleSpecification rule = CreateSshRule(FirewallAddressFamily.IPv4);
        DeleteRuleRequest request = IntentRequestFactory.CreateDeleteRequest(
            key,
            DEPLOYMENT_ID,
            new DeleteRulePayload { RuleId = "sha256:not-the-real-id", Rule = rule },
            MessageJsonSerializerContext.Default.DeleteRulePayload,
            clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        Assert.IsInstanceOfType<IntentVerificationResult.Rejected>(verifier.VerifyDelete(request));
    }

    [TestMethod]
    public void VerifyDelete_AcceptsMatchingRuleId()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        FirewallRuleSpecification rule = CreateSshRule(FirewallAddressFamily.IPv4);
        DeleteRuleRequest request = IntentRequestFactory.CreateDeleteRequest(
            key,
            DEPLOYMENT_ID,
            new DeleteRulePayload { RuleId = RuleIdentity.Compute(rule), Rule = rule },
            MessageJsonSerializerContext.Default.DeleteRulePayload,
            clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        Assert.IsInstanceOfType<IntentVerificationResult.Accepted>(verifier.VerifyDelete(request));
    }

    [TestMethod]
    public void VerifyDelete_RejectsFamilyNeutralRule()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        FirewallRuleSpecification rule = CreateSshRule();
        DeleteRuleRequest request = IntentRequestFactory.CreateDeleteRequest(
            key,
            DEPLOYMENT_ID,
            new DeleteRulePayload { RuleId = RuleIdentity.Compute(rule), Rule = rule },
            MessageJsonSerializerContext.Default.DeleteRulePayload,
            clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        AssertRejected<BadRequestResponse>(verifier.VerifyDelete(request));
    }

    [TestMethod]
    public void Canonicalize_IsStableAcrossEquivalentRules()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRulePayload left = new()
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
        AddRulePayload right = new()
        {
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                Direction = FirewallDirection.In,
                Protocol = FirewallProtocol.Tcp,
                Source = "any",
                DestinationPorts = "22,80",
            }
        };
        AddRuleRequest request = IntentRequestFactory.CreateAddRequest(
            key,
            DEPLOYMENT_ID,
            left,
            MessageJsonSerializerContext.Default.AddRulePayload,
            clock);

        CollectionAssert.AreEqual(
            IntentCanonicalizer.CanonicalizeAdd(request, left),
            IntentCanonicalizer.CanonicalizeAdd(request, right));
    }

    [TestMethod]
    public void Canonicalize_BindsAddressFamily()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRulePayload ipv4 = new()
        {
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                AddressFamily = FirewallAddressFamily.IPv4,
                Direction = FirewallDirection.In,
                DestinationPorts = "22",
            }
        };
        AddRulePayload ipv6 = new()
        {
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                AddressFamily = FirewallAddressFamily.IPv6,
                Direction = FirewallDirection.In,
                DestinationPorts = "22",
            }
        };
        AddRuleRequest request = IntentRequestFactory.CreateAddRequest(
            key,
            DEPLOYMENT_ID,
            ipv4,
            MessageJsonSerializerContext.Default.AddRulePayload,
            clock);

        Assert.IsFalse(IntentCanonicalizer.CanonicalizeAdd(request, ipv4)
            .SequenceEqual(IntentCanonicalizer.CanonicalizeAdd(request, ipv6)));
    }

    private static FirewallRuleSpecification CreateSshRule(
        FirewallAddressFamily addressFamily = FirewallAddressFamily.Any) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = addressFamily,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = "22",
    };

    private static AddRuleRequest SignAdd(ECDsa key, TimeProvider clock, string deploymentId = DEPLOYMENT_ID) =>
        IntentRequestFactory.CreateAddRequest(
            key,
            deploymentId,
            new AddRulePayload { Rule = CreateSshRule() },
            MessageJsonSerializerContext.Default.AddRulePayload,
            clock);

    private static IntentVerifier CreateVerifier(ECDsa authorizedKey, TimeProvider clock, string deploymentId = DEPLOYMENT_ID)
    {
        StaticAuthorizedKeyStore keys = new(authorizedKey);
        TestConfiguration configuration = new(TestAppSettingsFactory.Create());
        return new IntentVerifier(
            keys,
            new StaticDeploymentIdentityProvider(deploymentId),
            configuration,
            clock,
            MessageJsonSerializerContext.Default);
    }

    private static void AssertRejected<TResponse>(IntentVerificationResult result) where TResponse : IResponsePayload
    {
        Assert.IsInstanceOfType<IntentVerificationResult.Rejected>(result);
        IntentVerificationResult.Rejected rejected = (IntentVerificationResult.Rejected)result;
        Assert.IsInstanceOfType<TResponse>(rejected.Response);
    }

    private sealed class StaticAuthorizedKeyStore(ECDsa key) : IAuthorizedKeyStore
    {
        public bool TryGetKey(string keyId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ECDsa? found)
        {
            if (string.Equals(keyId, IntentSigner.ComputeKeyId(key), StringComparison.Ordinal))
            {
                found = key;
                return true;
            }

            found = null;
            return false;
        }
    }

    private sealed class StaticDeploymentIdentityProvider(string deploymentId) : IDeploymentIdentityProvider
    {
        public string GetDeploymentId() => deploymentId;
    }
}
