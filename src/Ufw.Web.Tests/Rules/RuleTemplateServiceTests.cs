using Ufw.Shared.Firewall;
using Ufw.Web.Model.V1.RuleTemplates;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Tests.Rules;

[TestClass]
public sealed class RuleTemplateServiceTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task CreateAsync_NormalizesReusableAuthoringStateAsync()
    {
        Guid firstTag = Guid.Parse("0199a100-0000-7000-8000-000000000001");
        Guid secondTag = Guid.Parse("0199a100-0000-7000-8000-000000000002");
        Guid group = Guid.Parse("0199a100-0000-7000-8000-000000000003");
        FakeRuleTemplateRepository repository = new();
        RuleTemplateService service = new(repository, new RuleMetadataValuesNormalizer());

        RuleTemplateMutationResult result = await service.CreateAsync(new CreateRuleTemplateRequest
        {
            Name = "  Web ingress  ",
            Description = "  reusable ingress  ",
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                AddressFamily = FirewallAddressFamily.Any,
                Direction = FirewallDirection.Forward,
                Protocol = FirewallProtocol.Tcp,
                Source = " 10.0.0.25/24 ",
                SourcePorts = "443,80",
                SourceInterface = " lan0 ",
                Destination = " 192.0.2.25 ",
                DestinationPorts = " 8443 ",
                DestinationInterface = " dmz0 ",
                Comment = "  web ingress  ",
            },
            Notes = "  operations note  ",
            TagIds = [secondTag, firstTag, secondTag],
            GroupId = group,
        }, TestContext.CancellationToken);

        Assert.AreEqual(RuleTemplateMutationOutcome.Success, result.Outcome);
        Assert.IsNotNull(repository.CreatedValues);
        RuleTemplateValues values = repository.CreatedValues;
        Assert.AreEqual("Web ingress", values.Name);
        Assert.AreEqual("reusable ingress", values.Description);
        Assert.AreEqual(FirewallAddressFamily.IPv4, values.Rule.AddressFamily);
        Assert.AreEqual("10.0.0.0/24", values.Rule.Source);
        Assert.AreEqual("80,443", values.Rule.SourcePorts);
        Assert.AreEqual("lan0", values.Rule.SourceInterface);
        Assert.AreEqual("192.0.2.25", values.Rule.Destination);
        Assert.AreEqual("8443", values.Rule.DestinationPorts);
        Assert.AreEqual("dmz0", values.Rule.DestinationInterface);
        Assert.AreEqual("web ingress", values.Rule.Comment);
        Assert.AreEqual("operations note", values.Metadata.Notes);
        CollectionAssert.AreEqual(new[] { firstTag, secondTag }, values.Metadata.TagIds.ToArray());
        Assert.AreEqual(group, values.Metadata.GroupId);
    }

    [TestMethod]
    public async Task CreateAsync_ContextFreeIpv6AndUnknownInterfaceAreAcceptedAsync()
    {
        FakeRuleTemplateRepository repository = new();
        RuleTemplateService service = new(repository, new RuleMetadataValuesNormalizer());

        RuleTemplateMutationResult result = await service.CreateAsync(new CreateRuleTemplateRequest
        {
            Name = "Future IPv6",
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                AddressFamily = FirewallAddressFamily.IPv6,
                Direction = FirewallDirection.In,
                Protocol = FirewallProtocol.Tcp,
                Source = "2001:db8::/64",
                Destination = "any",
                DestinationPorts = "443",
                DestinationInterface = "future0",
            },
        }, TestContext.CancellationToken);

        Assert.AreEqual(RuleTemplateMutationOutcome.Success, result.Outcome);
        Assert.IsNotNull(repository.CreatedValues);
        RuleTemplateValues values = repository.CreatedValues;
        Assert.AreEqual(FirewallAddressFamily.IPv6, values.Rule.AddressFamily);
        Assert.AreEqual("future0", values.Rule.DestinationInterface);
    }

    [TestMethod]
    public async Task CreateAsync_InvalidRuleDoesNotReachRepositoryAsync()
    {
        FakeRuleTemplateRepository repository = new();
        RuleTemplateService service = new(repository, new RuleMetadataValuesNormalizer());

        RuleTemplateMutationResult result = await service.CreateAsync(new CreateRuleTemplateRequest
        {
            Name = "Invalid",
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                Direction = FirewallDirection.In,
                Protocol = FirewallProtocol.Tcp,
                SourcePorts = "70000",
            },
        }, TestContext.CancellationToken);

        Assert.AreEqual(RuleTemplateMutationOutcome.InvalidTemplate, result.Outcome);
        Assert.IsNull(repository.CreatedValues);
    }

    [TestMethod]
    [DataRow(RuleTemplateLimits.MAX_DESCRIPTION_LENGTH, true)]
    [DataRow(RuleTemplateLimits.MAX_DESCRIPTION_LENGTH + 1, false)]
    public async Task CreateAsync_EnforcesDescriptionLengthAsync(int descriptionLength, bool expectedValid)
    {
        FakeRuleTemplateRepository repository = new();
        RuleTemplateService service = new(repository, new RuleMetadataValuesNormalizer());

        RuleTemplateMutationResult result = await service.CreateAsync(new CreateRuleTemplateRequest
        {
            Name = "Description boundary",
            Description = new string('x', descriptionLength),
            Rule = ValidRule(),
        }, TestContext.CancellationToken);

        Assert.AreEqual(expectedValid ? RuleTemplateMutationOutcome.Success : RuleTemplateMutationOutcome.InvalidTemplate, result.Outcome);
        Assert.AreEqual(expectedValid, repository.CreatedValues is not null);
    }

    [TestMethod]
    public async Task UpdateAsync_InvalidMetadataDoesNotReachRepositoryAsync()
    {
        FakeRuleTemplateRepository repository = new();
        RuleTemplateService service = new(repository, new RuleMetadataValuesNormalizer());

        RuleTemplateMutationResult result = await service.UpdateAsync(Guid.CreateVersion7(), new UpdateRuleTemplateRequest
        {
            Name = "Invalid metadata",
            Rule = ValidRule(),
            TagIds = [Guid.Empty],
        }, TestContext.CancellationToken);

        Assert.AreEqual(RuleTemplateMutationOutcome.InvalidTemplate, result.Outcome);
        Assert.IsNull(repository.UpdatedValues);
    }

    private static FirewallRuleSpecification ValidRule() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.Any,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = "any",
        Destination = "any",
        DestinationPorts = "443",
    };

    private sealed class FakeRuleTemplateRepository : IRuleTemplateRepository
    {
        public RuleTemplateValues? CreatedValues { get; private set; }

        public RuleTemplateValues? UpdatedValues { get; private set; }

        public Task<RuleTemplateInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RuleTemplateInventoryResponse());

        public Task<RuleTemplateMutationResult> CreateAsync(RuleTemplateValues values, CancellationToken cancellationToken = default)
        {
            CreatedValues = values;
            return Task.FromResult(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.Success, new RuleTemplateInventoryResponse()));
        }

        public Task<RuleTemplateMutationResult> UpdateAsync(Guid publicId, RuleTemplateValues values, CancellationToken cancellationToken = default)
        {
            _ = publicId;
            UpdatedValues = values;
            return Task.FromResult(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.Success, new RuleTemplateInventoryResponse()));
        }

        public Task<RuleTemplateMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            _ = publicId;
            return Task.FromResult(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.Success, new RuleTemplateInventoryResponse()));
        }
    }
}
