using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.V1.RuleMetadata;
using Ufw.Web.Model.V1.RuleTags;
using Ufw.Web.Data;
using Ufw.Web.Tests.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.Rules;
using Wkg.AspNetCore.Transactions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Tests.Rules;

[TestClass]
public sealed class RuleMetadataServiceTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task UpdateAndInventory_NormalizeMetadataAndRetainItWhenRuleDisappearsOutOfBandAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live", "sha256:other");
        RuleTagItem production = await host.CreateTagAsync(" Production ", "#12ab34", TestContext.CancellationToken);
        RuleTagItem ssh = await host.CreateTagAsync("SSH", "#AABBCC", TestContext.CancellationToken);

        RuleMetadataUpdateResult updated = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest
            {
                Notes = " managed by platform ",
                TagIds = [production.Id, production.Id, ssh.Id],
            },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleMetadataUpdateOutcome.Success, updated.Outcome);
        Assert.IsNotNull(updated.Response?.Metadata);
        Assert.AreEqual('7', updated.Response.Metadata.Id.ToString("D")[14]);
        Assert.AreEqual("managed by platform", updated.Response.Metadata.Notes);
        Assert.HasCount(2, updated.Response.Metadata.Tags);
        Assert.AreEqual("Production", updated.Response.Metadata.Tags[0].Name);
        Assert.AreEqual("#12AB34", updated.Response.Metadata.Tags[0].Color);
        Assert.AreEqual("SSH", updated.Response.Metadata.Tags[1].Name);
        Assert.AreEqual(1, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(2, await host.MetadataTagRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(2, await host.RuleTagRowCountAsync(TestContext.CancellationToken));

        RuleInventoryResponse inventory = await host.Inventory.GetAsync(TestContext.CancellationToken);
        Assert.HasCount(2, inventory.Firewall.Rules);
        Assert.HasCount(1, inventory.Metadata);
        Assert.AreEqual("sha256:live", inventory.Metadata[0].RuleId);
        Assert.AreEqual(updated.Response.Metadata.Id, inventory.Metadata[0].Id);

        host.SetRules("sha256:other");
        RuleInventoryResponse afterExternalRemoval = await host.Inventory.GetAsync(TestContext.CancellationToken);

        Assert.IsEmpty(afterExternalRemoval.Metadata);
        Assert.AreEqual(1, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(2, await host.RuleTagRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Inventory_ReadWithoutMetadataIsSideEffectFreeAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");

        RuleInventoryResponse inventory = await host.Inventory.GetAsync(TestContext.CancellationToken);

        Assert.HasCount(1, inventory.Firewall.Rules);
        Assert.IsEmpty(inventory.Metadata);
        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Update_RejectsMissingRuleInvalidMetadataAndUnknownTagsWithoutPersistingAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");

        RuleMetadataUpdateResult missing = await host.Metadata.UpdateAsync(
            "sha256:missing",
            new UpdateRuleMetadataRequest { Notes = "edge" },
            TestContext.CancellationToken);
        RuleMetadataUpdateResult invalid = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { TagIds = Enumerable.Repeat(Guid.CreateVersion7(), 33).ToArray() },
            TestContext.CancellationToken);
        RuleMetadataUpdateResult unknownTag = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { TagIds = [Guid.CreateVersion7()] },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleMetadataUpdateOutcome.RuleNotFound, missing.Outcome);
        Assert.AreEqual(RuleMetadataUpdateOutcome.InvalidMetadata, invalid.Outcome);
        Assert.AreEqual(RuleMetadataUpdateOutcome.TagNotFound, unknownTag.Outcome);
        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Update_EmptyMetadataRemovesRelationButPreservesReusableTagAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");
        RuleTagItem production = await host.CreateTagAsync("prod", "#112233", TestContext.CancellationToken);
        _ = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { TagIds = [production.Id] },
            TestContext.CancellationToken);

        RuleMetadataUpdateResult cleared = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { Notes = "  ", TagIds = [] },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleMetadataUpdateOutcome.Success, cleared.Outcome);
        Assert.IsNull(cleared.Response?.Metadata);
        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(0, await host.MetadataTagRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(1, await host.RuleTagRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RuleTags_UseUuidV7IdentityAndCannotBeDeletedWhileAttachedAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");

        RuleTagMutationResult created = await host.Tags.CreateAsync(
            new CreateRuleTagRequest { Name = "  Production  ", Color = "#12ab34" },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleTagMutationOutcome.Success, created.Outcome);
        RuleTagItem tag = created.Inventory!.Tags.Single();
        Assert.AreEqual('7', tag.Id.ToString("D")[14]);
        Assert.AreEqual("Production", tag.Name);
        Assert.AreEqual("#12AB34", tag.Color);

        RuleTagMutationResult duplicate = await host.Tags.CreateAsync(
            new CreateRuleTagRequest { Name = "PRODUCTION", Color = "#FFFFFF" },
            TestContext.CancellationToken);
        Assert.AreEqual(RuleTagMutationOutcome.NameConflict, duplicate.Outcome);

        RuleTagMutationResult updated = await host.Tags.UpdateAsync(
            tag.Id,
            new UpdateRuleTagRequest { Name = "Prod", Color = "#0011aa" },
            TestContext.CancellationToken);
        Assert.AreEqual(RuleTagMutationOutcome.Success, updated.Outcome);
        RuleTagItem renamed = updated.Inventory!.Tags.Single();
        Assert.AreEqual(tag.Id, renamed.Id);
        Assert.AreEqual("Prod", renamed.Name);
        Assert.AreEqual("#0011AA", renamed.Color);

        _ = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { TagIds = [tag.Id] },
            TestContext.CancellationToken);

        RuleTagMutationResult inUse = await host.Tags.DeleteAsync(tag.Id, TestContext.CancellationToken);
        Assert.AreEqual(RuleTagMutationOutcome.InUse, inUse.Outcome);
        Assert.AreEqual(1, await host.RuleTagRowCountAsync(TestContext.CancellationToken));

        _ = await host.Metadata.UpdateAsync("sha256:live", new UpdateRuleMetadataRequest(), TestContext.CancellationToken);
        RuleTagMutationResult deleted = await host.Tags.DeleteAsync(tag.Id, TestContext.CancellationToken);

        Assert.AreEqual(RuleTagMutationOutcome.Success, deleted.Outcome);
        Assert.IsEmpty(deleted.Inventory!.Tags);
        Assert.AreEqual(0, await host.RuleTagRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Reconciliation_DiscoveryIsSideEffectFreeAndReturnsOnlyUnmatchedMetadataAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live", "sha256:orphan");
        _ = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { Notes = "still attached" },
            TestContext.CancellationToken);
        _ = await host.Metadata.UpdateAsync(
            "sha256:orphan",
            new UpdateRuleMetadataRequest { Notes = "review me" },
            TestContext.CancellationToken);
        host.SetRules("sha256:live");

        RuleMetadataReconciliationResponse response = await host.Reconciliation.GetAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, response.RemovedCount);
        Assert.HasCount(1, response.Orphans);
        Assert.AreEqual("sha256:orphan", response.Orphans[0].RuleId);
        Assert.AreEqual("review me", response.Orphans[0].Notes);
        Assert.AreEqual(2, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Reconciliation_CleanupRemovesOnlySelectedRecordsThatAreStillUnmatchedAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:reattached", "sha256:remove", "sha256:keep");
        RuleMetadataItem reattached = (await host.Metadata.UpdateAsync(
            "sha256:reattached",
            new UpdateRuleMetadataRequest { Notes = "reattached" },
            TestContext.CancellationToken)).Response!.Metadata!;
        RuleMetadataItem remove = (await host.Metadata.UpdateAsync(
            "sha256:remove",
            new UpdateRuleMetadataRequest { Notes = "remove" },
            TestContext.CancellationToken)).Response!.Metadata!;
        RuleMetadataItem keep = (await host.Metadata.UpdateAsync(
            "sha256:keep",
            new UpdateRuleMetadataRequest { Notes = "keep" },
            TestContext.CancellationToken)).Response!.Metadata!;
        host.SetRules();

        RuleMetadataReconciliationResponse discovered = await host.Reconciliation.GetAsync(TestContext.CancellationToken);
        Assert.HasCount(3, discovered.Orphans);

        host.SetRules("sha256:reattached");
        RuleMetadataReconciliationResponse cleaned = await host.Reconciliation.CleanupAsync(
            new CleanupRuleMetadataRequest { MetadataIds = [reattached.Id, remove.Id] },
            TestContext.CancellationToken);

        Assert.AreEqual(1, cleaned.RemovedCount);
        Assert.HasCount(1, cleaned.Orphans);
        Assert.AreEqual(keep.Id, cleaned.Orphans[0].Id);
        Assert.AreEqual(2, await host.MetadataRowCountAsync(TestContext.CancellationToken));

        RuleInventoryResponse inventory = await host.Inventory.GetAsync(TestContext.CancellationToken);
        Assert.HasCount(1, inventory.Metadata);
        Assert.AreEqual(reattached.Id, inventory.Metadata[0].Id);
    }

    [TestMethod]
    public async Task RemoveForDeletedRule_RemovesMetadataForInBandDeletionAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");
        _ = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { Notes = "temporary" },
            TestContext.CancellationToken);

        await host.Metadata.RemoveForDeletedRuleAsync("sha256:live", TestContext.CancellationToken);

        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;
        private readonly AsyncServiceScope _scope;
        private readonly TestDaemonRuleSource _daemon;
        private readonly ApplicationDbContext _context;

        private TestHost(
            SqliteConnection connection,
            ServiceProvider services,
            AsyncServiceScope scope,
            TestDaemonRuleSource daemon,
            ApplicationDbContext context,
            RuleMetadataService metadata,
            RuleInventoryService inventory,
            RuleMetadataReconciliationService reconciliation,
            RuleTagService tags)
        {
            _connection = connection;
            _services = services;
            _scope = scope;
            _daemon = daemon;
            _context = context;
            Metadata = metadata;
            Inventory = inventory;
            Reconciliation = reconciliation;
            Tags = tags;
        }

        public RuleMetadataService Metadata { get; }

        public RuleInventoryService Inventory { get; }

        public RuleMetadataReconciliationService Reconciliation { get; }

        public RuleTagService Tags { get; }

        public static async Task<TestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<IModelLoader, SqliteApplicationModelLoader>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddTransactionManagement<ApplicationDbContext>(options =>
                options.UseIsolationLevel(IsolationLevel.ReadCommitted));

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            TestDaemonRuleSource daemon = new();
            ITransactionServiceHandle transactionHandle = scope.ServiceProvider.GetRequiredService<ITransactionServiceHandle>();
            RuleMetadataRepository metadataRepository = new(transactionHandle);
            RuleTagRepository tagRepository = new(transactionHandle);
            RuleMetadataService metadata = new(daemon, metadataRepository, scope.ServiceProvider.GetRequiredService<ILogger<RuleMetadataService>>());
            RuleInventoryService inventory = new(daemon, metadataRepository);
            RuleMetadataReconciliationService reconciliation = new(daemon, metadataRepository);
            RuleTagService tags = new(tagRepository);
            return new TestHost(connection, serviceProvider, scope, daemon, context, metadata, inventory, reconciliation, tags);
        }

        public void SetRules(params string[] ruleIds)
        {
            ListedFirewallRule[] rules = [.. ruleIds.Select(static (ruleId, index) => new ListedFirewallRule
            {
                RuleId = ruleId,
                DisplayNumber = index + 1,
                Parsed = true,
                RawLine = ruleId,
                Rule = new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4 },
            })];
            _daemon.Response = new RuleListResponse(true, rules, TestFirewallConfiguration.Enabled);
        }

        public async Task<RuleTagItem> CreateTagAsync(string name, string color, CancellationToken cancellationToken)
        {
            RuleTagMutationResult result = await Tags.CreateAsync(new CreateRuleTagRequest { Name = name, Color = color }, cancellationToken);
            Assert.AreEqual(RuleTagMutationOutcome.Success, result.Outcome);
            Assert.IsNotNull(result.Inventory);
            return result.Inventory.Tags.Single(tag => string.Equals(tag.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public async Task<int> MetadataRowCountAsync(CancellationToken cancellationToken)
        {
            _context.ChangeTracker.Clear();
            return await _context.Set<RuleMetadataEntry>().CountAsync(cancellationToken);
        }

        public async Task<int> MetadataTagRowCountAsync(CancellationToken cancellationToken)
        {
            _context.ChangeTracker.Clear();
            return await _context.Set<RuleMetadataTagEntry>().CountAsync(cancellationToken);
        }

        public async Task<int> RuleTagRowCountAsync(CancellationToken cancellationToken)
        {
            _context.ChangeTracker.Clear();
            return await _context.Set<RuleTagEntry>().CountAsync(cancellationToken);
        }

        private sealed class TestDaemonRuleSource : IDaemonRuleSource
        {
            public RuleListResponse Response { get; set; } = new(true, [], TestFirewallConfiguration.Enabled);

            public Task<RuleListResponse> GetAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Response);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
