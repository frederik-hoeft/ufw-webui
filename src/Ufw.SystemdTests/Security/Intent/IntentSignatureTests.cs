using System.Security.Cryptography;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Ipc.Shared.Serialization.Json;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Security.Intent;

[TestClass]
public sealed class IntentSignatureTests
{
    [TestMethod]
    public void VerifyAdd_AcceptsFreshSignatureFromAuthorizedKey()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(key, clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        IntentVerificationResult result = verifier.VerifyAdd(request);

        Assert.IsInstanceOfType<IntentVerificationResult.Accepted>(result);
    }

    [TestMethod]
    public void VerifyAdd_RejectsTamperedPayload()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(key, clock);
        AddRuleRequest tampered = request with
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

        IntentVerificationResult result = verifier.VerifyAdd(tampered);

        Assert.IsInstanceOfType<IntentVerificationResult.Rejected>(result);
        IntentVerificationResult.Rejected rejected = (IntentVerificationResult.Rejected)result;
        Assert.IsInstanceOfType<Ufw.Ipc.Shared.Model.Responses.ForbiddenResponse>(rejected.Response);
    }

    [TestMethod]
    public void VerifyAdd_RejectsUnknownKey()
    {
        using ECDsa signer = IntentSigner.CreateP256();
        using ECDsa authorized = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(signer, clock);
        IntentVerifier verifier = CreateVerifier(authorized, clock);

        IntentVerificationResult result = verifier.VerifyAdd(request);

        Assert.IsInstanceOfType<IntentVerificationResult.Rejected>(result);
    }

    [TestMethod]
    public void VerifyAdd_RejectsExpiredIntent()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        AddRuleRequest request = SignAdd(key, clock);
        clock.Advance(TimeSpan.FromMinutes(10));
        IntentVerifier verifier = CreateVerifier(key, clock);

        IntentVerificationResult result = verifier.VerifyAdd(request);

        Assert.IsInstanceOfType<IntentVerificationResult.Rejected>(result);
    }

    [TestMethod]
    public void VerifyDelete_RejectsMismatchedRuleId()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        FirewallRuleSpecification rule = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            DestinationPorts = "22",
        };
        DeleteRuleRequest request = IntentRequestFactory.CreateDeleteRequest(
            key,
            new DeleteRulePayload { RuleId = "sha256:not-the-real-id", Rule = rule },
            MessageJsonSerializerContext.Default.DeleteRulePayload,
            clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        IntentVerificationResult result = verifier.VerifyDelete(request);

        Assert.IsInstanceOfType<IntentVerificationResult.Rejected>(result);
    }

    [TestMethod]
    public void VerifyDelete_AcceptsMatchingRuleId()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        FirewallRuleSpecification rule = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            DestinationPorts = "22",
        };
        DeleteRuleRequest request = IntentRequestFactory.CreateDeleteRequest(
            key,
            new DeleteRulePayload { RuleId = RuleIdentity.Compute(rule), Rule = rule },
            MessageJsonSerializerContext.Default.DeleteRulePayload,
            clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        IntentVerificationResult result = verifier.VerifyDelete(request);

        Assert.IsInstanceOfType<IntentVerificationResult.Accepted>(result);
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
            left,
            MessageJsonSerializerContext.Default.AddRulePayload,
            clock);

        byte[] first = IntentCanonicalizer.CanonicalizeAdd(request, left);
        byte[] second = IntentCanonicalizer.CanonicalizeAdd(request, right);
        CollectionAssert.AreEqual(first, second);
    }

    private static AddRuleRequest SignAdd(ECDsa key, TimeProvider clock) =>
        IntentRequestFactory.CreateAddRequest(
            key,
            new AddRulePayload
            {
                Rule = new FirewallRuleSpecification
                {
                    Action = FirewallAction.Allow,
                    Direction = FirewallDirection.In,
                    Protocol = FirewallProtocol.Tcp,
                    DestinationPorts = "22",
                }
            },
            MessageJsonSerializerContext.Default.AddRulePayload,
            clock);

    private static IntentVerifier CreateVerifier(ECDsa authorizedKey, TimeProvider clock)
    {
        StaticAuthorizedKeyStore keys = new(authorizedKey);
        TestConfiguration configuration = new(TestAppSettingsFactory.Create());
        return new IntentVerifier(keys, configuration, clock, MessageJsonSerializerContext.Default);
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
}
