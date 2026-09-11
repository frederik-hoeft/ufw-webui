using Moq;
using Ufw.Client.Api;
using Ufw.Client.Intent;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Tests.Api;

[TestClass]
public sealed class RuleMutationServiceTests
{
    [TestMethod]
    public async Task AddRuleAsync_UsesCurrentCompatibleDeploymentContextAsync()
    {
        TestHost host = new();
        FirewallRuleSpecification rule = new() { Action = FirewallAction.Allow };
        AddRuleRequest signed = CreateAddRequest();
        RuleMutationResponse expected = new(IntentOperations.ADD_RULE, new ListedFirewallRule());
        host.Context.Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION, "deployment"));
        host.Signer.Setup(service => service.CreateAddRuleRequestAsync("deployment", rule, "key", It.IsAny<CancellationToken>())).ReturnsAsync(signed);
        host.Rules.Setup(client => client.AddRuleAsync(signed, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        RuleMutationResponse actual = await host.Service.AddRuleAsync(rule, "key");

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public async Task AddRuleAsync_ProtocolMismatchStopsBeforeSigningOrMutationAsync()
    {
        TestHost host = new();
        host.Context.Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION + 1, "deployment"));

        ApiProtocolException exception = await Assert.ThrowsExactlyAsync<ApiProtocolException>(() =>
            host.Service.AddRuleAsync(new FirewallRuleSpecification(), "key"));

        StringAssert.Contains(exception.Message, "protocol mismatch", StringComparison.OrdinalIgnoreCase);
        host.Signer.VerifyNoOtherCalls();
        host.Rules.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeleteRuleAsync_RequiresParsedRuleStableIdAndSpecificationAsync()
    {
        TestHost host = new();
        ListedFirewallRule[] invalidRules =
        [
            new() { Parsed = false, RuleId = "id", Rule = new FirewallRuleSpecification() },
            new() { Parsed = true, RuleId = null, Rule = new FirewallRuleSpecification() },
            new() { Parsed = true, RuleId = "id", Rule = null },
        ];

        foreach (ListedFirewallRule invalid in invalidRules)
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.Service.DeleteRuleAsync(invalid, "key"));
        }

        host.Context.VerifyNoOtherCalls();
        host.Signer.VerifyNoOtherCalls();
        host.Rules.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeleteRuleAsync_BindsStableIdentityAndSpecificationAsync()
    {
        TestHost host = new();
        FirewallRuleSpecification specification = new() { Action = FirewallAction.Deny };
        ListedFirewallRule rule = new() { Parsed = true, RuleId = "stable-id", Rule = specification };
        DeleteRuleRequest signed = CreateDeleteRequest();
        RuleMutationResponse expected = new(IntentOperations.DELETE_RULE, rule);
        host.Context.Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION, "deployment"));
        host.Signer.Setup(service => service.CreateDeleteRuleRequestAsync("deployment", "stable-id", specification, "key", It.IsAny<CancellationToken>())).ReturnsAsync(signed);
        host.Rules.Setup(client => client.DeleteRuleAsync(signed, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        RuleMutationResponse actual = await host.Service.DeleteRuleAsync(rule, "key");

        Assert.AreSame(expected, actual);
    }

    private static AddRuleRequest CreateAddRequest() => new()
    {
        DeploymentId = "deployment",
        KeyId = "key-id",
        Nonce = "nonce",
        Operation = IntentOperations.ADD_RULE,
        Payload = default,
        Signature = "signature",
    };

    private static DeleteRuleRequest CreateDeleteRequest() => new()
    {
        DeploymentId = "deployment",
        KeyId = "key-id",
        Nonce = "nonce",
        Operation = IntentOperations.DELETE_RULE,
        Payload = default,
        Signature = "signature",
    };

    private sealed class TestHost
    {
        public Mock<IRuleApiClient> Rules { get; } = new(MockBehavior.Strict);

        public Mock<IIntentContextApiClient> Context { get; } = new(MockBehavior.Strict);

        public Mock<IIntentSigningService> Signer { get; } = new(MockBehavior.Strict);

        public RuleMutationService Service => new(Rules.Object, Context.Object, Signer.Object);
    }
}
