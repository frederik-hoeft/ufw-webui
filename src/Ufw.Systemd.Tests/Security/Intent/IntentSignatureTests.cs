using System.Security.Cryptography;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
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
    public void VerifyInsert_AcceptsFreshSignatureFromAuthorizedKey()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        InsertRuleRequest request = SignInsert(key, clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        IntentVerificationResult.AcceptedInsertion accepted =
            Assert.IsInstanceOfType<IntentVerificationResult.AcceptedInsertion>(verifier.VerifyInsert(request));
        Assert.AreEqual(CreateInsertPayload().BaselineFingerprint, accepted.Payload.BaselineFingerprint);
        Assert.AreEqual(1, accepted.Payload.AnchorOccurrenceId);
        Assert.AreEqual(RuleInsertionPlacement.After, accepted.Payload.Placement);
        Assert.AreEqual(FirewallAddressFamily.IPv4, accepted.Payload.Rule.AddressFamily);
    }

    [TestMethod]
    public void VerifyInsert_RejectsTamperedStateConditionOrRule()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        InsertRuleRequest signed = SignInsert(key, clock);
        IntentVerifier verifier = CreateVerifier(key, clock);
        InsertRulePayload baseline = CreateInsertPayload();
        InsertRulePayload[] tampered =
        [
            CreateInsertPayload(baselineFingerprint: FirewallRuleSnapshotFingerprint.Compute(active: false, [])),
            CreateInsertPayload(anchorOccurrenceId: 2),
            CreateInsertPayload(placement: RuleInsertionPlacement.Before),
            CreateInsertPayload(rule: CreateRule(FirewallAddressFamily.IPv4, "443")),
        ];

        foreach (InsertRulePayload payload in tampered)
        {
            InsertRuleRequest request = signed with
            {
                Payload = System.Text.Json.JsonSerializer.SerializeToElement(
                    payload,
                    MessageJsonSerializerContext.Default.InsertRulePayload),
            };
            AssertRejected<ForbiddenResponse>(verifier.VerifyInsert(request));
        }
    }

    [TestMethod]
    public void VerifyInsert_RejectsOperationSubstitutionAndTamperedDeployment()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        InsertRuleRequest request = SignInsert(key, clock);

        AssertRejected<BadRequestResponse>(
            CreateVerifier(key, clock).VerifyInsert(request with { Operation = IntentOperations.ADD_RULE }));
        AssertRejected<ForbiddenResponse>(
            CreateVerifier(key, clock, deploymentId: "deployment-b")
                .VerifyInsert(request with { DeploymentId = "deployment-b" }));
    }

    [TestMethod]
    public void VerifyInsert_RejectsMalformedOrFamilyNeutralPayloadBeforeSignatureVerification()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        InsertRuleRequest request = SignInsert(key, clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        InsertRulePayload malformedFingerprint = CreateInsertPayload(baselineFingerprint: "sha256:not-a-digest");
        request = request with
        {
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(
                malformedFingerprint,
                MessageJsonSerializerContext.Default.InsertRulePayload),
        };
        AssertRejected<BadRequestResponse>(verifier.VerifyInsert(request));

        InsertRulePayload familyNeutral = CreateInsertPayload(rule: CreateRule(FirewallAddressFamily.Any, "22"));
        request = request with
        {
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(
                familyNeutral,
                MessageJsonSerializerContext.Default.InsertRulePayload),
        };
        AssertRejected<BadRequestResponse>(verifier.VerifyInsert(request));
    }

    [TestMethod]
    public void VerifyReorder_AcceptsFreshSignatureFromAuthorizedKey()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        ReorderRulesRequest request = SignReorder(key, clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        IntentVerificationResult.AcceptedReorder accepted =
            Assert.IsInstanceOfType<IntentVerificationResult.AcceptedReorder>(verifier.VerifyReorder(request));
        CollectionAssert.AreEqual(CreateReorderPayload().DesiredOrder, accepted.Payload.DesiredOrder);
    }

    [TestMethod]
    public void VerifyReorder_RejectsTamperedBaselineFingerprint()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        ReorderRulesRequest request = SignReorder(key, clock);
        ReorderRulesPayload tamperedPayload = CreateReorderPayload(
            baselineFingerprint: FirewallRuleSnapshotFingerprint.Compute(active: false, []));
        request = request with
        {
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(
                tamperedPayload,
                MessageJsonSerializerContext.Default.ReorderRulesPayload),
        };
        IntentVerifier verifier = CreateVerifier(key, clock);

        AssertRejected<ForbiddenResponse>(verifier.VerifyReorder(request));
    }

    [TestMethod]
    public void VerifyReorder_RejectsTamperedDesiredOrder()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        ReorderRulesRequest request = SignReorder(key, clock);
        ReorderRulesPayload tamperedPayload = CreateReorderPayload(desiredOrder: [0, 2, 1]);
        request = request with
        {
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(
                tamperedPayload,
                MessageJsonSerializerContext.Default.ReorderRulesPayload),
        };
        IntentVerifier verifier = CreateVerifier(key, clock);

        AssertRejected<ForbiddenResponse>(verifier.VerifyReorder(request));
    }

    [TestMethod]
    public void VerifyReorder_RejectsTamperedNonce()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        ReorderRulesRequest request = SignReorder(key, clock) with { Nonce = IntentSigner.CreateNonce() };
        IntentVerifier verifier = CreateVerifier(key, clock);

        AssertRejected<ForbiddenResponse>(verifier.VerifyReorder(request));
    }

    [TestMethod]
    public void VerifyReorder_RejectsOperationSubstitution()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        ReorderRulesRequest request = SignReorder(key, clock) with { Operation = IntentOperations.ADD_RULE };
        IntentVerifier verifier = CreateVerifier(key, clock);

        AssertRejected<BadRequestResponse>(verifier.VerifyReorder(request));
    }

    [TestMethod]
    public void VerifyReorder_RejectsTamperedDeploymentIdentity()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        ReorderRulesRequest request = SignReorder(key, clock) with { DeploymentId = "deployment-b" };
        IntentVerifier verifier = CreateVerifier(key, clock, deploymentId: "deployment-b");

        AssertRejected<ForbiddenResponse>(verifier.VerifyReorder(request));
    }

    [TestMethod]
    public void VerifyReorder_RejectsMalformedBaselineFingerprint()
    {
        using ECDsa key = IntentSigner.CreateP256();
        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        ReorderRulesPayload payload = CreateReorderPayload(baselineFingerprint: "sha256:not-a-digest");
        ReorderRulesRequest request = IntentRequestFactory.CreateReorderRequest(
            key,
            DEPLOYMENT_ID,
            payload,
            MessageJsonSerializerContext.Default.ReorderRulesPayload,
            clock);
        IntentVerifier verifier = CreateVerifier(key, clock);

        AssertRejected<BadRequestResponse>(verifier.VerifyReorder(request));
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
        AddRuleRequest request = IntentRequestFactory.CreateAddRequest(key, DEPLOYMENT_ID, left, MessageJsonSerializerContext.Default.AddRulePayload, clock);

        CollectionAssert.AreEqual(IntentCanonicalizer.CanonicalizeAdd(request, left), IntentCanonicalizer.CanonicalizeAdd(request, right));
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
        AddRuleRequest request = IntentRequestFactory.CreateAddRequest(key, DEPLOYMENT_ID, ipv4, MessageJsonSerializerContext.Default.AddRulePayload, clock);

        Assert.IsFalse(IntentCanonicalizer.CanonicalizeAdd(request, ipv4)
            .SequenceEqual(IntentCanonicalizer.CanonicalizeAdd(request, ipv6)));
    }

    private static InsertRulePayload CreateInsertPayload(
        string? baselineFingerprint = null,
        int anchorOccurrenceId = 1,
        RuleInsertionPlacement placement = RuleInsertionPlacement.After,
        FirewallRuleSpecification? rule = null) => new()
        {
            BaselineFingerprint = baselineFingerprint ?? FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            AnchorOccurrenceId = anchorOccurrenceId,
            Placement = placement,
            Rule = rule ?? CreateRule(FirewallAddressFamily.IPv4, "22"),
        };

    private static InsertRuleRequest SignInsert(ECDsa key, TimeProvider clock) =>
        IntentRequestFactory.CreateInsertRequest(
            key,
            DEPLOYMENT_ID,
            CreateInsertPayload(),
            MessageJsonSerializerContext.Default.InsertRulePayload,
            clock);

    private static FirewallRuleSpecification CreateRule(FirewallAddressFamily family, string port) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = family,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = port,
    };

    private static ReorderRulesPayload CreateReorderPayload(
        string? baselineFingerprint = null,
        int[]? desiredOrder = null) => new()
        {
            BaselineFingerprint = baselineFingerprint ?? FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            DesiredOrder = desiredOrder ?? [2, 0, 1],
        };

    private static ReorderRulesRequest SignReorder(ECDsa key, TimeProvider clock) =>
        IntentRequestFactory.CreateReorderRequest(
            key,
            DEPLOYMENT_ID,
            CreateReorderPayload(),
            MessageJsonSerializerContext.Default.ReorderRulesPayload,
            clock);

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
        return new IntentVerifier(keys, new StaticDeploymentIdentityProvider(deploymentId), configuration, clock, MessageJsonSerializerContext.Default);
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
