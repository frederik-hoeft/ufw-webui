using Moq;
using Ufw.Client.Api;
using Ufw.Client.Intent;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Tests.Api;

[TestClass]
public sealed class RuleOrderingServiceTests
{
    [TestMethod]
    public async Task ApplyAsync_FingerprintsExactDisplayedBaselineAndSignsCompletePermutationAsync()
    {
        RuleListResponse baseline = new(true, [Rule("dup", 1), Rule("dup", 2)]);
        int[] desiredOrder = [1, 0];
        RuleReorderResponse expected = Response(RuleReorderOutcome.PartiallyCompleted, baseline);
        Mock<IRuleApiClient> api = new();
        Mock<IIntentContextApiClient> context = new();
        Mock<IIntentSigningService> signer = new();
        context.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION, "deployment"));
        ReorderRulesRequest signed = Request();
        signer.Setup(candidate => candidate.CreateReorderRulesRequestAsync(
                "deployment",
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<int>>(),
                "private-key",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(signed);
        api.Setup(candidate => candidate.ReorderRulesAsync(signed, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        RuleOrderingService service = new(api.Object, context.Object, signer.Object);

        RuleReorderResponse actual = await service.ApplyAsync(baseline, desiredOrder, "private-key");

        Assert.AreSame(expected, actual);
        Assert.AreEqual(RuleReorderOutcome.PartiallyCompleted, actual.Outcome);
        string expectedFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        signer.Verify(candidate => candidate.CreateReorderRulesRequestAsync(
            "deployment",
            expectedFingerprint,
            It.Is<IReadOnlyList<int>>(order => order.SequenceEqual(desiredOrder)),
            "private-key",
            It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(candidate => candidate.ReorderRulesAsync(signed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ApplyAsync_ProtocolMismatchStopsSigningAndSubmissionAsync()
    {
        Mock<IRuleApiClient> api = new();
        Mock<IIntentContextApiClient> context = new();
        Mock<IIntentSigningService> signer = new();
        context.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION + 1, "deployment"));
        RuleOrderingService service = new(api.Object, context.Object, signer.Object);

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() =>
            service.ApplyAsync(new RuleListResponse(true, [Rule("a", 1)]), [0], "private-key"));

        signer.VerifyNoOtherCalls();
        api.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ApplyAsync_InvalidPermutationFailsBeforeContextOrCryptoAsync()
    {
        Mock<IRuleApiClient> api = new();
        Mock<IIntentContextApiClient> context = new();
        Mock<IIntentSigningService> signer = new();
        RuleOrderingService service = new(api.Object, context.Object, signer.Object);
        RuleListResponse baseline = new(true, [Rule("a", 1), Rule("b", 2)]);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.ApplyAsync(baseline, [0], "private-key"));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.ApplyAsync(baseline, [0, 0], "private-key"));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.ApplyAsync(baseline, [0, 2], "private-key"));

        context.VerifyNoOtherCalls();
        signer.VerifyNoOtherCalls();
        api.VerifyNoOtherCalls();
    }

    private static ListedFirewallRule Rule(string id, int number) => new()
    {
        RuleId = id,
        DisplayNumber = number,
        Parsed = true,
        RawLine = id,
        Rule = new FirewallRuleSpecification(),
    };

    private static ReorderRulesRequest Request() => new()
    {
        DeploymentId = "deployment",
        KeyId = "key",
        Nonce = "nonce",
        Operation = IntentOperations.REORDER_RULES,
        Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { baselineFingerprint = "sha256:test", desiredOrder = new[] { 1, 0 } }),
        Signature = "signature",
    };

    private static RuleReorderResponse Response(RuleReorderOutcome outcome, RuleListResponse snapshot) => new(
        outcome,
        snapshot,
        [],
        [],
        [],
        Diagnostic: null);
}
