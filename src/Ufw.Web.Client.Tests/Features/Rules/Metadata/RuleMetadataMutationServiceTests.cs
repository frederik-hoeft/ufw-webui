using Moq;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules.Metadata;

[TestClass]
public sealed class RuleMetadataMutationServiceTests
{
    [TestMethod]
    public async Task UpdateAsync_BuildsRequestAndPreservesIdentityAndCancellationAsync()
    {
        using CancellationTokenSource lifetime = new();
        Guid tagId = Guid.CreateVersion7();
        Guid groupId = Guid.CreateVersion7();
        RuleMetadataChange change = new("notes", [tagId], groupId);
        RuleMetadataMutationResponse response = new(new RuleMetadataItem { Id = Guid.CreateVersion7(), RuleId = "rule", Tags = [] });
        Mock<IRuleApiClient> api = new(MockBehavior.Strict);
        api.Setup(client => client.UpdateMetadataAsync("rule", It.Is<UpdateRuleMetadataRequest>(request =>
            request.Notes == "notes" && request.TagIds.SequenceEqual(new[] { tagId }) && request.GroupId == groupId), lifetime.Token)).ReturnsAsync(response);
        RuleMetadataMutationService service = new(api.Object);

        RuleMetadataMutationResponse actual = await service.UpdateAsync("rule", change, lifetime.Token);

        Assert.AreSame(response, actual);
        api.VerifyAll();
    }

    [TestMethod]
    public async Task UpdateAsync_AcceptsClearedMetadataAsync()
    {
        Mock<IRuleApiClient> api = new();
        api.Setup(client => client.UpdateMetadataAsync("rule", It.IsAny<UpdateRuleMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataMutationResponse());

        RuleMetadataMutationResponse result = await new RuleMetadataMutationService(api.Object).UpdateAsync("rule", new RuleMetadataChange(null, [], null));

        Assert.IsNull(result.Metadata);
    }

    [TestMethod]
    public async Task UpdateAsync_RejectsMalformedOrMismatchedResponsesAsync()
    {
        Mock<IRuleApiClient> api = new();
        api.SetupSequence(client => client.UpdateMetadataAsync("rule", It.IsAny<UpdateRuleMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataMutationResponse(new RuleMetadataItem { Id = Guid.CreateVersion7(), RuleId = "different", Tags = [] }))
            .ReturnsAsync(new RuleMetadataMutationResponse(new RuleMetadataItem { Id = Guid.Empty, RuleId = "rule", Tags = [] }));
        RuleMetadataMutationService service = new(api.Object);
        RuleMetadataChange change = new(null, [], null);

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => service.UpdateAsync("rule", change));
        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => service.UpdateAsync("rule", change));
    }

    [TestMethod]
    public async Task UpdateAsync_RejectsInvalidArgumentsBeforeCallingApiAsync()
    {
        Mock<IRuleApiClient> api = new(MockBehavior.Strict);
        RuleMetadataMutationService service = new(api.Object);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.UpdateAsync("", new RuleMetadataChange(null, [], null)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.UpdateAsync("rule", null!));
        api.VerifyNoOtherCalls();
    }
}
