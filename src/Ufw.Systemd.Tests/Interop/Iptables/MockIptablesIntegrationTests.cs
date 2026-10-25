using Ufw.Mock;
using Ufw.Mock.Iptables;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.SystemState;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Interop.Iptables.Grammars;

namespace Ufw.Systemd.Tests.Interop.Iptables;

/// <summary>Exercises the mock's observable command and file boundaries through the production parsers.</summary>
[TestClass]
[DoNotParallelize]
public sealed class MockIptablesIntegrationTests
{
    private string _directory = null!;
    private Dictionary<string, string?> _originalEnvironment = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "MockIptablesIntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _originalEnvironment = new Dictionary<string, string?>(StringComparer.Ordinal);
        SetEnvironment("UFW_MOCK_STATE_PATH", Path.Combine(_directory, "state.json"));
        SetEnvironment("UFW_MOCK_USER_RULES_PATH", Path.Combine(_directory, "user.rules"));
        SetEnvironment("UFW_MOCK_USER6_RULES_PATH", Path.Combine(_directory, "user6.rules"));
        SetEnvironment("UFW_MOCK_IPTABLES4_BEFORE_FILES", null);
        SetEnvironment("UFW_MOCK_IPTABLES4_AFTER_FILES", null);
        SetEnvironment("UFW_MOCK_IPTABLES6_BEFORE_FILES", null);
        SetEnvironment("UFW_MOCK_IPTABLES6_AFTER_FILES", null);
        SetEnvironment("UFW_DEFAULTS_PATH", Path.Combine(_directory, "defaults"));
        File.WriteAllText(Path.Combine(_directory, "defaults"), "IPV6=yes\n");
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach ((string name, string? value) in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
        Directory.Delete(_directory, recursive: true);
    }

    [TestMethod]
    public async Task GeneratedFilesAndPhysicalRulesParseForBothFamiliesAsync()
    {
        Assert.AreEqual(0, (await InvokeUfwAsync("allow", "in", "from", "any", "to", "any", "port", "22", "proto", "tcp", "comment", "ssh")).ExitCode);
        Assert.AreEqual(0, (await InvokeUfwAsync("route", "deny", "in", "on", "eth0", "out", "on", "eth1", "from", "10.0.0.0/8", "to", "192.0.2.0/24")).ExitCode);
        Assert.AreEqual(0, (await InvokeUfwAsync("--force", "enable")).ExitCode);

        (int statusCode, string statusOutput, string statusError) = await InvokeUfwAsync("status", "numbered");
        Assert.AreEqual(0, statusCode, statusError);
        UfwStatusSnapshot? numbered = UfwStatusParser.Parse(statusOutput);
        Assert.IsNotNull(numbered);
        IReadOnlyList<ListedFirewallRule> observed = numbered.Rules.Select(UfwRuleMapper.ToListedRule).ToArray();
        Assert.IsTrue(observed.All(static rule => rule.Parsed), "The UFW mock's own numbered rows must be interpretable by the daemon.");

        foreach ((FirewallAddressFamily family, string file) in new[]
        {
            (FirewallAddressFamily.IPv4, "user.rules"),
            (FirewallAddressFamily.IPv6, "user6.rules"),
        })
        {
            (int code, string save, string stderr) = InvokeSave(family, "-t", "filter");
            Assert.AreEqual(0, code, stderr);
            Assert.IsTrue(IptablesSaveGrammar.Instance.TryParse(save, family, out RawFilterTable? table));
            Assert.IsTrue(UfwUserRulesGrammar.Instance.TryParse(await File.ReadAllTextAsync(Path.Combine(_directory, file)), family, out IReadOnlyList<UfwUserRuleTranslation>? translations));
            Assert.AreEqual(family == FirewallAddressFamily.IPv4 ? 2 : 1, translations.Count);
            IReadOnlyList<UfwUserRuleTranslation> linked = UfwManagedRuleLinker.Link(translations, observed, family);
            Assert.AreEqual(translations.Count, linked.Count, "UFW's independent numbered output must link every mock translation.");
            RawFilterTable opposite = new() { AddressFamily = family == FirewallAddressFamily.IPv4 ? FirewallAddressFamily.IPv6 : FirewallAddressFamily.IPv4, Chains = [] };
            FilterSystemState snapshot = family == FirewallAddressFamily.IPv4
                ? FilterSystemStateFactory.Create(table, opposite, linked)
                : FilterSystemStateFactory.Create(opposite, table, ipv6UserRules: linked);
            AddressFamilyFilterState familyState = family == FirewallAddressFamily.IPv4 ? snapshot.IPv4 : snapshot.IPv6;
            Assert.AreEqual(FilterEnrichmentStatus.Complete, familyState.Enrichment.Status);
            Assert.AreEqual(linked.Sum(static translation => translation.IptablesRules.Count), familyState.ManagedRuleAttributions.Count);
            Assert.IsTrue(translations.All(static translation => translation.RuleId is not null && translation.IptablesRules.Count > 0));
            Assert.AreEqual(translations.Sum(static translation => translation.IptablesRules.Count), table.Chains.Where(static chain => chain.Name.StartsWith("ufw-user-", StringComparison.Ordinal) && chain.Name is not "ufw-user-limit" and not "ufw-user-limit-accept").Sum(static chain => chain.Rules.Count));
            Assert.IsTrue(table.Chains.Single(static chain => chain.Name == "INPUT").Rules.Any(static rule => rule.Target.Name == "ufw-user-input"));
        }
    }

    [TestMethod]
    public async Task ExpandedLimitLoggingMultiportAndAnyProtocolRulesReconcileAsync()
    {
        Assert.AreEqual(0, (await InvokeUfwAsync("allow", "in", "from", "any", "to", "any", "port", "80,443", "proto", "tcp", "comment", "hello")).ExitCode);
        Assert.AreEqual(0, (await InvokeUfwAsync("limit", "2222/tcp")).ExitCode);
        Assert.AreEqual(0, (await InvokeUfwAsync("allow", "out", "log", "from", "any", "to", "192.0.2.0/24", "port", "53", "proto", "udp")).ExitCode);
        Assert.AreEqual(0, (await InvokeUfwAsync("deny", "in", "from", "any", "to", "any", "port", "4444")).ExitCode);
        Assert.AreEqual(0, (await InvokeUfwAsync("--force", "enable")).ExitCode);

        (int exit, string status, _) = await InvokeUfwAsync("status", "numbered");
        Assert.AreEqual(0, exit);
        UfwStatusSnapshot? numbered = UfwStatusParser.Parse(status);
        Assert.IsNotNull(numbered);
        IReadOnlyList<ListedFirewallRule> listed = numbered.Rules.Select(UfwRuleMapper.ToListedRule).ToArray();
        Assert.IsTrue(listed.All(static item => item.Parsed));

        (exit, string save, _) = InvokeSave(FirewallAddressFamily.IPv4, "-t", "filter");
        Assert.AreEqual(0, exit);
        Assert.IsTrue(IptablesSaveGrammar.Instance.TryParse(save, FirewallAddressFamily.IPv4, out RawFilterTable? table));
        string rules = await File.ReadAllTextAsync(Path.Combine(_directory, "user.rules"));
        Assert.IsTrue(UfwUserRulesGrammar.Instance.TryParse(rules, FirewallAddressFamily.IPv4, out IReadOnlyList<UfwUserRuleTranslation>? translations));
        Assert.AreEqual(4, translations.Count);
        IReadOnlyList<UfwUserRuleTranslation> linked = UfwManagedRuleLinker.Link(translations, listed, FirewallAddressFamily.IPv4);
        Assert.AreEqual(4, linked.Count);
        RawFilterTable ipv6 = new() { AddressFamily = FirewallAddressFamily.IPv6, Chains = [] };
        FilterSystemState result = FilterSystemStateFactory.Create(table, ipv6, linked);
        Assert.AreEqual(FilterEnrichmentStatus.Complete, result.IPv4.Enrichment.Status);
        Assert.AreEqual(linked.Sum(static translation => translation.IptablesRules.Count), result.IPv4.ManagedRuleAttributions.Count);
    }

    [TestMethod]
    public async Task ExternalRulesAreActiveAndAreNotMisattributedAsUfwManagedAsync()
    {
        string before = Path.Combine(_directory, "before.rules");
        await File.WriteAllTextAsync(before, """
            # UFW-style before rules, with an additional table intentionally ignored.
            *nat
            :PREROUTING ACCEPT [0:0]
            COMMIT
            *filter
            :ufw-before-input - [0:0]
            :external-chain - [0:0]
            -A ufw-before-input -s 198.51.100.0/24 -j external-chain
            -A external-chain -p udp --dport 5353 -j DROP
            COMMIT
            """);
        SetEnvironment("UFW_MOCK_IPTABLES4_BEFORE_FILES", before);
        Assert.AreEqual(0, (await InvokeUfwAsync("allow", "22/tcp")).ExitCode);
        Assert.AreEqual(0, (await InvokeUfwAsync("--force", "enable")).ExitCode);

        (int code, string save, string stderr) = InvokeSave(FirewallAddressFamily.IPv4, "-t", "filter");
        Assert.AreEqual(0, code, stderr);
        Assert.IsTrue(IptablesSaveGrammar.Instance.TryParse(save, FirewallAddressFamily.IPv4, out RawFilterTable? table));
        Assert.AreEqual(1, table.Chains.Single(static chain => chain.Name == "external-chain").Rules.Count);
        Assert.IsTrue(table.Chains.Single(static chain => chain.Name == "ufw-before-input").Rules.Single().Target.IsChainReference);
        Assert.HasCount(1, table.Chains.Single(static chain => chain.Name == "ufw-user-input").Rules);
        Assert.IsFalse((await File.ReadAllTextAsync(Path.Combine(_directory, "user.rules"))).Contains("external-chain", StringComparison.Ordinal));
        Assert.IsFalse(InvokeSave(FirewallAddressFamily.IPv6, "-t", "filter").StdOut.Contains("external-chain", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GeneratedFilesTrackMutationsButNotDryRunsAsync()
    {
        Assert.AreEqual(0, (await InvokeUfwAsync("allow", "22/tcp")).ExitCode);
        string userPath = Path.Combine(_directory, "user.rules");
        string user6Path = Path.Combine(_directory, "user6.rules");
        string first4 = await File.ReadAllTextAsync(userPath);
        string first6 = await File.ReadAllTextAsync(user6Path);
        Assert.Contains("--dport 22", first4);
        Assert.Contains("--dport 22", first6);

        Assert.AreEqual(0, (await InvokeUfwAsync("--dry-run", "allow", "443/tcp")).ExitCode);
        Assert.AreEqual(first4, await File.ReadAllTextAsync(userPath));
        Assert.AreEqual(first6, await File.ReadAllTextAsync(user6Path));

        Assert.AreEqual(0, (await InvokeUfwAsync("--force", "delete", "1")).ExitCode);
        Assert.IsFalse((await File.ReadAllTextAsync(userPath)).Contains("--dport 22", StringComparison.Ordinal));
        Assert.Contains("--dport 22", await File.ReadAllTextAsync(user6Path));
    }

    [TestMethod]
    public async Task Ipv6DisabledOnlySuppressesActiveSaveNotStoredTranslationsAsync()
    {
        Assert.AreEqual(0, (await InvokeUfwAsync("allow", "22/tcp")).ExitCode);
        Assert.AreEqual(0, (await InvokeUfwAsync("--force", "enable")).ExitCode);
        string defaults = Path.Combine(_directory, "defaults");
        await File.WriteAllTextAsync(defaults, "IPV6=no\n");

        (int code, string save, _) = InvokeSave(FirewallAddressFamily.IPv6, "-t", "filter");
        Assert.AreEqual(0, code);
        Assert.IsTrue(IptablesSaveGrammar.Instance.TryParse(save, FirewallAddressFamily.IPv6, out RawFilterTable? inactiveV6));
        Assert.HasCount(0, inactiveV6.Chains.Single(static chain => chain.Name == "ufw-user-input").Rules);
        Assert.Contains("--dport 22", await File.ReadAllTextAsync(Path.Combine(_directory, "user6.rules")));

        await File.WriteAllTextAsync(defaults, "IPV6=yes\n");
        (code, save, _) = InvokeSave(FirewallAddressFamily.IPv6, "-t", "filter");
        Assert.AreEqual(0, code);
        Assert.IsTrue(IptablesSaveGrammar.Instance.TryParse(save, FirewallAddressFamily.IPv6, out RawFilterTable? enabledV6));
        Assert.HasCount(1, enabledV6.Chains.Single(static chain => chain.Name == "ufw-user-input").Rules);
    }

    [TestMethod]
    public async Task DisabledStateOmitsManagedPhysicalRulesAndInvalidFixtureFailsClosedAsync()
    {
        Assert.AreEqual(0, (await InvokeUfwAsync("allow", "22/tcp")).ExitCode);
        (int code, string save, string stderr) = InvokeSave(FirewallAddressFamily.IPv4, "-t", "filter");
        Assert.AreEqual(0, code, stderr);
        Assert.IsTrue(IptablesSaveGrammar.Instance.TryParse(save, FirewallAddressFamily.IPv4, out RawFilterTable? table));
        Assert.HasCount(0, table.Chains.Single(static chain => chain.Name == "ufw-user-input").Rules);

        string fixture = Path.Combine(_directory, "invalid.rules");
        await File.WriteAllTextAsync(fixture, "*filter\n:INPUT DROP [0:0]\n-A missing -j ACCEPT\nCOMMIT\n");
        SetEnvironment("UFW_MOCK_IPTABLES4_BEFORE_FILES", fixture);
        (code, save, stderr) = InvokeSave(FirewallAddressFamily.IPv4, "-t", "filter");
        Assert.AreEqual(1, code);
        Assert.AreEqual(string.Empty, save);
        Assert.Contains("iptables-save:", stderr);
    }

    private void SetEnvironment(string key, string? value)
    {
        if (!_originalEnvironment.ContainsKey(key))
        {
            _originalEnvironment.Add(key, Environment.GetEnvironmentVariable(key));
        }
        Environment.SetEnvironmentVariable(key, value);
    }

    private static (int ExitCode, string StdOut, string StdErr) InvokeSave(FirewallAddressFamily family, params string[] args)
    {
        TextWriter stdout = Console.Out;
        TextWriter stderr = Console.Error;
        using StringWriter result = new();
        using StringWriter errors = new();
        try
        {
            Console.SetOut(result);
            Console.SetError(errors);
            int code = IptablesSaveApplication.Run(family, args);
            return (code, result.ToString(), errors.ToString());
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeUfwAsync(params string[] args)
    {
        TextWriter stdout = Console.Out;
        TextWriter stderr = Console.Error;
        using StringWriter result = new();
        using StringWriter errors = new();
        try
        {
            Console.SetOut(result);
            Console.SetError(errors);
            int code = await UfwMockApplication.RunAsync(args);
            return (code, result.ToString(), errors.ToString());
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }
}
