using System.Diagnostics.CodeAnalysis;
using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.SystemState;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.SystemState;

[TestClass]
public sealed class FilterSystemStateReaderTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ReadAsync_ResolvesSaveOutputAndEnrichesItFromUserRulesAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-system-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string userRules = Path.Combine(directory, "user.rules");
        string user6Rules = Path.Combine(directory, "user6.rules");
        await File.WriteAllTextAsync(userRules, """
            *filter
            :ufw-user-input - [0:0]
            ### RULES ###
            ### tuple ### allow tcp 22 0.0.0.0/0 any 0.0.0.0/0 in
            -A ufw-user-input -p tcp --dport 22 -j ACCEPT
            ### tuple ### allow any 80 0.0.0.0/0 any 0.0.0.0/0 in
            -A ufw-user-input -p tcp --dport 80 -j ACCEPT
            -A ufw-user-input -p udp --dport 80 -j ACCEPT
            ### END RULES ###
            COMMIT
            """, TestContext.CancellationToken);
        await File.WriteAllTextAsync(user6Rules, """
            *filter
            :ufw6-user-input - [0:0]
            ### RULES ###
            ### END RULES ###
            COMMIT
            """, TestContext.CancellationToken);

        const string ipv4Save = """
            *filter
            :INPUT DROP [0:0]
            :FORWARD DROP [0:0]
            :OUTPUT ACCEPT [0:0]
            :ufw-before-input - [0:0]
            :ufw-user-input - [0:0]
            -A INPUT -j ufw-before-input
            -A ufw-before-input -i lo -j ACCEPT
            -A ufw-before-input -j ufw-user-input
            -A ufw-user-input -p tcp -m tcp --dport 22 -j ACCEPT
            -A ufw-user-input -p tcp -m tcp --dport 80 -j ACCEPT
            -A ufw-user-input -p udp -m udp --dport 80 -j ACCEPT
            COMMIT
            """;
        const string ipv6Save = """
            *filter
            :INPUT ACCEPT [0:0]
            :FORWARD ACCEPT [0:0]
            :OUTPUT ACCEPT [0:0]
            COMMIT
            """;

        try
        {
            FilterSystemStateQueryService service = CreateService(ipv4Save, ipv6Save, userRules, user6Rules, out _);
            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

            FilterSystemStateResponse response = (FilterSystemStateResponse)await service.GetAsync(cancellation.Token);

            ResolvedFilterRule[] linked = response.State.IPv4.Input.Rules.Where(static rule => rule.UserRule is not null).ToArray();
            Assert.HasCount(3, linked);
            UfwUserRuleReference ssh = linked[0].UserRule!;
            UfwUserRuleReference webTcp = linked[1].UserRule!;
            UfwUserRuleReference webUdp = linked[2].UserRule!;
            Assert.AreEqual(0, ssh.UserRuleIndex);
            Assert.AreEqual(1, ssh.TranslationCount);
            Assert.AreEqual(1, webTcp.UserRuleIndex);
            Assert.AreEqual(0, webTcp.TranslationIndex);
            Assert.AreEqual(2, webTcp.TranslationCount);
            Assert.AreEqual(1, webUdp.TranslationIndex);
            Assert.AreEqual(FirewallProtocol.Any, webTcp.Specification!.Protocol);
            Assert.AreEqual("ufw-user-input", linked[0].ProvenanceChain);
            CollectionAssert.AreEqual(new[] { "INPUT", "ufw-before-input", "ufw-user-input" }, linked[0].ChainPath.ToArray());
            Assert.IsTrue(response.State.IPv4.Input.Rules.Any(static rule => rule.ProvenanceChain == "ufw-before-input" && rule.UserRule is null));
            Assert.IsEmpty(response.State.IPv6.Input.Rules);
            Assert.AreEqual(FilterChainPolicy.Accept, response.State.IPv6.Input.Policy);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAsync_MissingUserRulesAreEmptyEnrichmentAsync()
    {
        FilterSystemStateQueryService service = CreateService(MinimalSave("DROP"), MinimalSave("ACCEPT"), "/missing/user.rules", "/missing/user6.rules", out _);

        FilterSystemStateResponse response = (FilterSystemStateResponse)await service.GetAsync(TestContext.CancellationToken);

        Assert.HasCount(1, response.State.IPv4.Input.Rules);
        Assert.IsNull(response.State.IPv4.Input.Rules[0].UserRule);
    }

    [TestMethod]
    public async Task ReadAsync_FailedSaveOrMalformedUserRulesFailTheReadAsync()
    {
        FilterSystemStateQueryService failedSave = CreateService(MinimalSave("ACCEPT"), MinimalSave("ACCEPT"), "/missing/user.rules", "/missing/user6.rules", out Mock<IChildProcessRunner> processes);
        processes.Setup(runner => runner.RunAsync(It.Is<ChildProcessRequest>(request => request.Command.Contains("ip6", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChildProcessResult(1, string.Empty, "nft unavailable", false));

        InternalServerErrorResponse saveError = (InternalServerErrorResponse)await failedSave.GetAsync(TestContext.CancellationToken);
        StringAssert.Contains(saveError.Message, "IPv6");

        string directory = Path.Combine(Path.GetTempPath(), "ufw-system-state-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string userRules = Path.Combine(directory, "user.rules");
        await File.WriteAllTextAsync(userRules, "this is not a rules file\n", TestContext.CancellationToken);
        try
        {
            FilterSystemStateQueryService malformed = CreateService(MinimalSave("ACCEPT"), MinimalSave("ACCEPT"), userRules, "/missing/user6.rules", out _);
            InternalServerErrorResponse rulesError = (InternalServerErrorResponse)await malformed.GetAsync(TestContext.CancellationToken);
            StringAssert.Contains(rulesError.Message, "IPv4");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The query service retains the gate until the test completes.")]
    private static FilterSystemStateQueryService CreateService(
        string ipv4Save,
        string ipv6Save,
        string userRulesPath,
        string user6RulesPath,
        out Mock<IChildProcessRunner> processes)
    {
        string ipv4Path = "/usr/sbin/iptables-save";
        string ipv6Path = "/usr/sbin/ip6tables-save";
        processes = new Mock<IChildProcessRunner>();
        processes.Setup(runner => runner.RunAsync(It.Is<ChildProcessRequest>(request => request.Command == ipv4Path), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChildProcessResult(0, ipv4Save, string.Empty, false));
        processes.Setup(runner => runner.RunAsync(It.Is<ChildProcessRequest>(request => request.Command == ipv6Path), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChildProcessResult(0, ipv6Save, string.Empty, false));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(
            iptablesSavePath: ipv4Path,
            ip6TablesSavePath: ipv6Path,
            ufwUserRulesPath: userRulesPath,
            ufwUser6RulesPath: user6RulesPath));
        FilterSystemStateReader reader = new(configuration, processes.Object, new ConsoleLogger());
        UfwExecutionGate gate = new();
        return new FilterSystemStateQueryService(reader, gate);
    }

    private static string MinimalSave(string inputPolicy) => $"""
        *filter
        :INPUT {inputPolicy} [0:0]
        :FORWARD ACCEPT [0:0]
        :OUTPUT ACCEPT [0:0]
        -A INPUT -j ACCEPT
        COMMIT
        """;
}
