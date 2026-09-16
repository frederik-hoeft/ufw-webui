using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Models.Rules;
using Ufw.Web.Data;
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

        RuleMetadataUpdateResult updated = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest
            {
                Group = " edge ",
                Notes = " managed by platform ",
                Tags = [" Prod ", "prod", "SSH"],
            },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleMetadataUpdateOutcome.Success, updated.Outcome);
        Assert.IsNotNull(updated.Response?.Metadata);
        Assert.AreEqual("edge", updated.Response.Metadata.Group);
        Assert.AreEqual("managed by platform", updated.Response.Metadata.Notes);
        CollectionAssert.AreEqual(new[] { "Prod", "SSH" }, updated.Response.Metadata.Tags.ToArray());
        Assert.AreEqual(1, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(2, await host.MetadataTagRowCountAsync(TestContext.CancellationToken));

        RuleInventoryResponse inventory = await host.Inventory.GetAsync(TestContext.CancellationToken);
        Assert.HasCount(2, inventory.Firewall.Rules);
        Assert.HasCount(1, inventory.Metadata);
        Assert.AreEqual("sha256:live", inventory.Metadata[0].RuleId);

        host.SetRules("sha256:other");
        RuleInventoryResponse afterExternalRemoval = await host.Inventory.GetAsync(TestContext.CancellationToken);

        Assert.IsEmpty(afterExternalRemoval.Metadata);
        Assert.AreEqual(1, await host.MetadataRowCountAsync(TestContext.CancellationToken));
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
    public async Task Update_RejectsMissingRuleAndInvalidMetadataWithoutPersistingAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");

        RuleMetadataUpdateResult missing = await host.Metadata.UpdateAsync(
            "sha256:missing",
            new UpdateRuleMetadataRequest { Group = "edge" },
            TestContext.CancellationToken);
        RuleMetadataUpdateResult invalid = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { Tags = Enumerable.Range(0, 33).Select(static index => $"tag-{index}").ToArray() },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleMetadataUpdateOutcome.RuleNotFound, missing.Outcome);
        Assert.AreEqual(RuleMetadataUpdateOutcome.InvalidMetadata, invalid.Outcome);
        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Update_EmptyMetadataRemovesExistingMetadataAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");
        _ = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { Group = "edge", Tags = ["prod"] },
            TestContext.CancellationToken);

        RuleMetadataUpdateResult cleared = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { Group = "  ", Notes = null, Tags = [] },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleMetadataUpdateOutcome.Success, cleared.Outcome);
        Assert.IsNull(cleared.Response?.Metadata);
        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(0, await host.MetadataTagRowCountAsync(TestContext.CancellationToken));
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
            RuleInventoryService inventory)
        {
            _connection = connection;
            _services = services;
            _scope = scope;
            _daemon = daemon;
            _context = context;
            Metadata = metadata;
            Inventory = inventory;
        }

        public RuleMetadataService Metadata { get; }

        public RuleInventoryService Inventory { get; }

        public static async Task<TestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<IModelLoader, ApplicationModelLoader>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddTransactionManagement<ApplicationDbContext>(options =>
                options.UseIsolationLevel(IsolationLevel.ReadCommitted));

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            TestDaemonRuleSource daemon = new();
            ITransactionServiceHandle transactionHandle = scope.ServiceProvider.GetRequiredService<ITransactionServiceHandle>();
            RuleMetadataRepository repository = new(transactionHandle);
            RuleMetadataService metadata = new(
                daemon,
                repository,
                scope.ServiceProvider.GetRequiredService<ILogger<RuleMetadataService>>());
            RuleInventoryService inventory = new(daemon, repository);
            return new TestHost(connection, serviceProvider, scope, daemon, context, metadata, inventory);
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
