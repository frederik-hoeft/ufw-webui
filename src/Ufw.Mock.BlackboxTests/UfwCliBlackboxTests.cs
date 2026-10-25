using System.Globalization;

namespace Ufw.Mock.BlackboxTests;

[TestClass]
[DoNotParallelize]
public sealed class UfwCliBlackboxTests
{
    private string _temporaryDirectory = null!;
    private string _statePath = null!;
    private string _defaultsPath = null!;
    private string? _originalDefaultsPath;

    public required TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "Ufw.Mock.BlackboxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
        _statePath = Path.Combine(_temporaryDirectory, "state.json");
        _defaultsPath = Path.Combine(_temporaryDirectory, "ufw-defaults");
        File.WriteAllText(_defaultsPath, "IPV6=yes\n");
        _originalDefaultsPath = Environment.GetEnvironmentVariable("UFW_DEFAULTS_PATH");
        Environment.SetEnvironmentVariable("UFW_DEFAULTS_PATH", _defaultsPath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("UFW_DEFAULTS_PATH", _originalDefaultsPath);
        try
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [TestMethod]
    public async Task FreshStateMatchesUfwInstallationDefaultsAsync()
    {
        CommandResult status = await InvokeAsync("status");
        Assert.AreEqual(0, status.ExitCode);
        Assert.AreEqual("Status: inactive", status.StdOut);
        Assert.AreEqual(string.Empty, status.StdErr);

        CommandResult enable = await InvokeAsync("--force", "enable");
        Assert.AreEqual(0, enable.ExitCode);
        Assert.Contains("Firewall is active and enabled on system startup", enable.StdOut);

        CommandResult verbose = await InvokeAsync("status", "verbose");
        Assert.Contains("Status: active", verbose.StdOut);
        Assert.Contains("Logging: on (low)", verbose.StdOut);
        Assert.Contains("Default: deny (incoming), allow (outgoing), deny (routed)", verbose.StdOut);
        Assert.Contains("New profiles: skip", verbose.StdOut);
    }

    [TestMethod]
    public async Task SystemdStyleRuleArgumentsMaterializeIpv4AndIpv6RowsAsync()
    {
        CommandResult add = await InvokeAsync("allow", "in", "from", "any", "to", "any", "port", "22", "proto", "tcp", "comment", "ssh");
        Assert.AreEqual(0, add.ExitCode);
        Assert.AreEqual("Rules updated\nRules updated (v6)", add.StdOut);

        _ = await InvokeAsync("--force", "enable");
        CommandResult status = await InvokeAsync("status", "numbered");
        Assert.Contains("[ 1] 22/tcp", status.StdOut);
        Assert.Contains("ALLOW IN", status.StdOut);
        Assert.Contains("Anywhere # ssh", status.StdOut);
        Assert.Contains("[ 2] 22/tcp (v6)", status.StdOut);
        Assert.Contains("Anywhere (v6) # ssh", status.StdOut);
        Assert.IsFalse(status.StdOut.Contains("Anywhere/tcp", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Ipv6DisabledMatchesUfwVisibilityAndMutationSemanticsAsync()
    {
        _ = await InvokeAsync("allow", "22/tcp");
        _ = await InvokeAsync("--force", "enable");

        CommandResult enabledStatus = await InvokeAsync("status", "numbered");
        Assert.Contains("22/tcp (v6)", enabledStatus.StdOut);

        await File.WriteAllTextAsync(_defaultsPath, "IPV6=no\n", TestContext.CancellationToken);

        CommandResult disabledStatus = await InvokeAsync("status", "numbered");
        Assert.Contains("[ 1] 22/tcp", disabledStatus.StdOut);
        Assert.IsFalse(disabledStatus.StdOut.Contains("(v6)", StringComparison.Ordinal));

        CommandResult addAny = await InvokeAsync("allow", "443/tcp");
        Assert.AreEqual(0, addAny.ExitCode);
        Assert.AreEqual("Rule added", addAny.StdOut);

        CommandResult addIpv6 = await InvokeAsync("allow", "from", "::/0", "to", "::/0", "port", "8443", "proto", "tcp");
        Assert.AreEqual(1, addIpv6.ExitCode);
        Assert.Contains("IPv6 support not enabled", addIpv6.StdErr);

        CommandResult disabledAdded = await InvokeAsync("show", "added");
        Assert.IsFalse(disabledAdded.StdOut.Contains("::/0", StringComparison.Ordinal));
        Assert.AreEqual(1, CountOccurrences(disabledAdded.StdOut, "22"));
        Assert.AreEqual(1, CountOccurrences(disabledAdded.StdOut, "443"));

        CommandResult delete = await InvokeAsync("--force", "delete", "1");
        Assert.AreEqual(0, delete.ExitCode);
        CommandResult afterDelete = await InvokeAsync("status", "numbered");
        Assert.Contains("[ 1] 443/tcp", afterDelete.StdOut);
        Assert.IsFalse(afterDelete.StdOut.Contains("22/tcp", StringComparison.Ordinal));

        await File.WriteAllTextAsync(_defaultsPath, "IPV6=yes\n", TestContext.CancellationToken);
        CommandResult reenabledStatus = await InvokeAsync("status", "numbered");
        Assert.Contains("22/tcp (v6)", reenabledStatus.StdOut);
        Assert.Contains("443/tcp", reenabledStatus.StdOut);
        Assert.AreEqual(1, CountOccurrences(reenabledStatus.StdOut, "443/tcp"));
    }

    [TestMethod]
    public async Task ForceIsRejectedForAddRuleSyntaxWithoutChangingStateAsync()
    {
        CommandResult add = await InvokeAsync("--force", "allow", "in", "from", "any", "to", "any", "port", "22", "proto", "tcp");

        Assert.AreEqual(1, add.ExitCode);
        Assert.AreEqual(string.Empty, add.StdOut);
        Assert.AreEqual("ERROR: Invalid syntax", add.StdErr);

        CommandResult added = await InvokeAsync("show", "added");
        Assert.IsFalse(added.StdOut.Contains("22", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ExplicitZeroNetworksRemainFamilySpecificAsync()
    {
        _ = await InvokeAsync("allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "80", "proto", "tcp");
        _ = await InvokeAsync(
            "allow",
            "from",
            "::/0",
            "to",
            "::/0",
            "port",
            "443",
            "proto",
            "tcp");
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        Assert.Contains("[ 1] 80/tcp", status.StdOut);
        Assert.Contains("[ 2] 443/tcp (v6)", status.StdOut);
        Assert.AreEqual(1, CountOccurrences(status.StdOut, "80/tcp"));
        Assert.AreEqual(1, CountOccurrences(status.StdOut, "443/tcp"));
    }

    [TestMethod]
    public async Task FullAndRoutedSyntaxProducesUfwStatusShapeAsync()
    {
        _ = await InvokeAsync("--force", "enable");
        CommandResult add = await InvokeAsync("route", "allow", "in", "on", "eth0", "out", "on", "eth1", "to", "10.0.0.0/8", "from", "192.168.0.0/16");
        Assert.AreEqual(0, add.ExitCode);
        Assert.AreEqual("Rule added", add.StdOut);

        CommandResult status = await InvokeAsync("status", "numbered");
        Assert.Contains("10.0.0.0/8 on eth1", status.StdOut);
        Assert.Contains("ALLOW FWD", status.StdOut);
        Assert.Contains("192.168.0.0/16 on eth0", status.StdOut);
    }

    [TestMethod]
    public async Task DeleteByRuleRemovesBothFamiliesWhileNumberDeletesOneRowAsync()
    {
        _ = await InvokeAsync("allow", "22/tcp");
        _ = await InvokeAsync("--force", "enable");

        CommandResult numberedDelete = await InvokeAsync("--force", "delete", "1");
        Assert.AreEqual("Rule deleted", numberedDelete.StdOut);
        CommandResult afterNumber = await InvokeAsync("status", "numbered");
        Assert.IsFalse(afterNumber.StdOut.Contains("[ 2]", StringComparison.Ordinal));
        Assert.Contains("22/tcp (v6)", afterNumber.StdOut);

        _ = await InvokeAsync("--force", "reset");
        _ = await InvokeAsync("allow", "22/tcp");
        _ = await InvokeAsync("--force", "enable");
        CommandResult syntaxDelete = await InvokeAsync("delete", "allow", "22/tcp");
        Assert.AreEqual(0, syntaxDelete.ExitCode);
        Assert.Contains("Rule deleted", syntaxDelete.StdOut);
        Assert.Contains("Rule deleted (v6)", syntaxDelete.StdOut);
        CommandResult afterSyntax = await InvokeAsync("status", "numbered");
        Assert.IsFalse(afterSyntax.StdOut.Contains("22/tcp", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task InsertAndPrependControlObservableRuleOrderAsync()
    {
        _ = await InvokeAsync("allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "80", "proto", "tcp");
        _ = await InvokeAsync("allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "443", "proto", "tcp");
        _ = await InvokeAsync("insert", "2", "deny", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp");
        _ = await InvokeAsync("prepend", "reject", "from", "1.2.3.4");
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        int reject = status.StdOut.IndexOf("1.2.3.4", StringComparison.Ordinal);
        int ssh = status.StdOut.IndexOf("22/tcp", StringComparison.Ordinal);
        int http = status.StdOut.IndexOf("80/tcp", StringComparison.Ordinal);
        int https = status.StdOut.IndexOf("443/tcp", StringComparison.Ordinal);
        Assert.IsLessThan(http, reject);
        Assert.IsLessThan(ssh, http);
        Assert.IsLessThan(https, ssh);
    }

    [TestMethod]
    public async Task RouteInsertControlsObservableRoutedRuleOrderAsync()
    {
        _ = await InvokeAsync("route", "allow", "in", "on", "eth0", "out", "on", "eth1", "from", "10.0.0.0/8", "to", "192.168.0.0/16", "port", "80", "proto", "tcp");
        _ = await InvokeAsync("route", "allow", "in", "on", "eth0", "out", "on", "eth1", "from", "10.0.0.0/8", "to", "192.168.0.0/16", "port", "443", "proto", "tcp");

        CommandResult inserted = await InvokeAsync(
            "route",
            "insert",
            "2",
            "deny",
            "in",
            "on",
            "eth0",
            "out",
            "on",
            "eth1",
            "from",
            "10.0.0.0/8",
            "to",
            "192.168.0.0/16",
            "port",
            "22",
            "proto",
            "tcp");
        Assert.AreEqual(0, inserted.ExitCode);
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        int http = status.StdOut.IndexOf("80/tcp", StringComparison.Ordinal);
        int ssh = status.StdOut.IndexOf("22/tcp", StringComparison.Ordinal);
        int https = status.StdOut.IndexOf("443/tcp", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, http);
        Assert.IsGreaterThan(http, ssh);
        Assert.IsGreaterThan(ssh, https);
        Assert.Contains("DENY FWD", status.StdOut);
    }

    [TestMethod]
    public async Task ProtocolOnlyRulesRetainProtocolInNumberedStatusAsync()
    {
        CommandResult tcp = await InvokeAsync("allow", "proto", "tcp", "from", "any", "to", "any");
        Assert.AreEqual(0, tcp.ExitCode);
        CommandResult udp = await InvokeAsync("route", "reject", "from", "0.0.0.0/0", "to", "10.100.200.2", "proto", "udp");
        Assert.AreEqual(0, udp.ExitCode);
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");

        Assert.Contains("Anywhere/tcp", status.StdOut);
        Assert.Contains("10.100.200.2/udp", status.StdOut);
        Assert.Contains("REJECT FWD", status.StdOut);
    }

    [TestMethod]
    public async Task InsertUsesCombinedNumberingForExplicitIpv6RulesAsync()
    {
        _ = await InvokeAsync("allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "80", "proto", "tcp");
        _ = await InvokeAsync("allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "443", "proto", "tcp");
        _ = await InvokeAsync("allow", "from", "::/0", "to", "::/0", "port", "80", "proto", "tcp");
        _ = await InvokeAsync("allow", "from", "::/0", "to", "::/0", "port", "443", "proto", "tcp");

        CommandResult inserted = await InvokeAsync(
            "insert",
            "4",
            "deny",
            "from",
            "::/0",
            "to",
            "::/0",
            "port",
            "22",
            "proto",
            "tcp");
        Assert.AreEqual(0, inserted.ExitCode);

        CommandResult wrongFamilySegment = await InvokeAsync(
            "insert",
            "2",
            "deny",
            "from",
            "::/0",
            "to",
            "::/0",
            "port",
            "25",
            "proto",
            "tcp");
        Assert.AreEqual(1, wrongFamilySegment.ExitCode);
        Assert.Contains("Invalid position '2'", wrongFamilySegment.StdErr);

        _ = await InvokeAsync("--force", "enable");
        CommandResult status = await InvokeAsync("status", "numbered");
        int httpV6 = status.StdOut.IndexOf("80/tcp (v6)", StringComparison.Ordinal);
        int sshV6 = status.StdOut.IndexOf("22/tcp (v6)", StringComparison.Ordinal);
        int httpsV6 = status.StdOut.IndexOf("443/tcp (v6)", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, httpV6);
        Assert.IsGreaterThan(httpV6, sshV6);
        Assert.IsGreaterThan(sshV6, httpsV6);
    }

    [TestMethod]
    public async Task FamilyNeutralInsertMirrorsCombinedAnchorAcrossFamilyPartitionsAsync()
    {
        _ = await InvokeAsync("allow", "80/tcp");
        _ = await InvokeAsync("allow", "443/tcp");

        CommandResult inserted = await InvokeAsync("insert", "1", "deny", "22/tcp");
        Assert.AreEqual(0, inserted.ExitCode);
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        int sshV4 = status.StdOut.IndexOf("22/tcp", StringComparison.Ordinal);
        int httpV4 = status.StdOut.IndexOf("80/tcp", StringComparison.Ordinal);
        int sshV6 = status.StdOut.IndexOf("22/tcp (v6)", StringComparison.Ordinal);
        int httpV6 = status.StdOut.IndexOf("80/tcp (v6)", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, sshV4);
        Assert.IsGreaterThan(sshV4, httpV4);
        Assert.IsGreaterThan(httpV4, sshV6);
        Assert.IsGreaterThan(sshV6, httpV6);
    }

    [TestMethod]
    public async Task FamilyNeutralInsertAtIpv6CombinedPositionMirrorsMatchingIpv4AnchorAsync()
    {
        _ = await InvokeAsync("allow", "80/tcp");
        _ = await InvokeAsync("allow", "443/tcp");

        CommandResult inserted = await InvokeAsync("insert", "4", "deny", "22/tcp");
        Assert.AreEqual(0, inserted.ExitCode);
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        int httpV4 = status.StdOut.IndexOf("80/tcp", StringComparison.Ordinal);
        int sshV4 = status.StdOut.IndexOf("22/tcp", StringComparison.Ordinal);
        int httpsV4 = status.StdOut.IndexOf("443/tcp", StringComparison.Ordinal);
        int httpV6 = status.StdOut.IndexOf("80/tcp (v6)", StringComparison.Ordinal);
        int sshV6 = status.StdOut.IndexOf("22/tcp (v6)", StringComparison.Ordinal);
        int httpsV6 = status.StdOut.IndexOf("443/tcp (v6)", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, httpV4);
        Assert.IsGreaterThan(httpV4, sshV4);
        Assert.IsGreaterThan(sshV4, httpsV4);
        Assert.IsGreaterThan(httpsV4, httpV6);
        Assert.IsGreaterThan(httpV6, sshV6);
        Assert.IsGreaterThan(sshV6, httpsV6);
    }

    [TestMethod]
    public async Task ExplicitIpv4InsertRejectsCombinedPositionInIpv6SegmentAsync()
    {
        _ = await InvokeAsync("allow", "80/tcp");

        CommandResult inserted = await InvokeAsync("insert", "2", "deny", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp");

        Assert.AreEqual(1, inserted.ExitCode);
        Assert.Contains("Invalid position '2'", inserted.StdErr);
    }

    [TestMethod]
    public async Task DuplicateRuleSkipsAndCommentChangeUpdatesExistingRuleAsync()
    {
        _ = await InvokeAsync("allow", "22/tcp", "comment", "old");
        CommandResult duplicate = await InvokeAsync("allow", "22/tcp", "comment", "old");
        Assert.AreEqual("Skipping adding existing rule\nSkipping adding existing rule (v6)", duplicate.StdOut);

        CommandResult update = await InvokeAsync("allow", "22/tcp", "comment", "new");
        Assert.AreEqual("Rules updated\nRules updated (v6)", update.StdOut);
        _ = await InvokeAsync("--force", "enable");
        CommandResult status = await InvokeAsync("status", "numbered");
        Assert.AreEqual(2, CountOccurrences(status.StdOut, "# new"));
        Assert.IsFalse(status.StdOut.Contains("# old", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task DryRunDoesNotPersistStateAsync()
    {
        CommandResult dryRun = await InvokeAsync("--dry-run", "enable");
        Assert.AreEqual(0, dryRun.ExitCode);
        Assert.Contains("Firewall is active", dryRun.StdOut);

        CommandResult status = await InvokeAsync("status");
        Assert.AreEqual("Status: inactive", status.StdOut);

        _ = await InvokeAsync("--dry-run", "allow", "22/tcp");
        _ = await InvokeAsync("--force", "enable");
        CommandResult numbered = await InvokeAsync("status", "numbered");
        Assert.IsFalse(numbered.StdOut.Contains("22/tcp", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task DefaultAndLoggingCommandsAreReflectedByVerboseStatusAsync()
    {
        _ = await InvokeAsync("default", "reject", "incoming");
        _ = await InvokeAsync("default", "deny", "outgoing");
        _ = await InvokeAsync("default", "allow", "routed");
        _ = await InvokeAsync("logging", "full");
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "verbose");
        Assert.Contains("Logging: on (full)", status.StdOut);
        Assert.Contains("Default: reject (incoming), deny (outgoing), allow (routed)", status.StdOut);

        _ = await InvokeAsync("logging", "off");
        CommandResult loggingOff = await InvokeAsync("status", "verbose");
        Assert.Contains("Logging: off", loggingOff.StdOut);
    }

    [TestMethod]
    public async Task InvalidRuleFailsWithoutPersistingItAsync()
    {
        CommandResult invalid = await InvokeAsync("allow", "proto", "gre", "to", "any", "port", "80");
        Assert.AreEqual(1, invalid.ExitCode);
        Assert.Contains("ERROR:", invalid.StdErr);
        Assert.Contains("cannot be combined with a port clause", invalid.StdErr);

        _ = await InvokeAsync("--force", "enable");
        CommandResult status = await InvokeAsync("status", "numbered");
        Assert.IsFalse(status.StdOut.Contains("80", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ApplicationProfilesUsePortablePersistedProfileStateAsync()
    {
        await File.WriteAllTextAsync(_statePath, """
            {
              "schemaVersion": 1,
              "applicationProfiles": [
                {
                  "name": "OpenSSH",
                  "title": "Secure shell server",
                  "description": "OpenSSH server",
                  "ports": "22/tcp"
                }
              ]
            }
            """, TestContext.CancellationToken);

        CommandResult list = await InvokeAsync("app", "list");
        Assert.Contains("Available applications:", list.StdOut);
        Assert.Contains("OpenSSH", list.StdOut);

        CommandResult info = await InvokeAsync("app", "info", "OpenSSH");
        Assert.Contains("Profile: OpenSSH", info.StdOut);
        Assert.Contains("Port:\n  22/tcp", info.StdOut);

        _ = await InvokeAsync("app", "default", "allow");
        CommandResult update = await InvokeAsync("app", "update", "--add-new", "OpenSSH");
        Assert.AreEqual(0, update.ExitCode);
        Assert.Contains("Rules updated for profile 'OpenSSH'", update.StdOut);
        _ = await InvokeAsync("--force", "enable");
        CommandResult status = await InvokeAsync("status", "numbered");
        Assert.Contains("OpenSSH", status.StdOut);
    }

    [TestMethod]
    public async Task AppUpdateAddNewRejectsAllProfileSelectorAsync()
    {
        await File.WriteAllTextAsync(_statePath, """
            {
              "schemaVersion": 1,
              "applicationProfiles": [
                {
                  "name": "One",
                  "ports": "80/tcp"
                },
                {
                  "name": "Two",
                  "ports": "443/tcp"
                }
              ]
            }
            """, TestContext.CancellationToken);

        CommandResult result = await InvokeAsync("app", "update", "--add-new", "all");
        Assert.AreEqual(1, result.ExitCode);
        Assert.Contains("Cannot specify 'all' with '--add-new'", result.StdErr);
    }

    [TestMethod]
    public async Task AllDocumentedShowReportsHaveDeterministicFacadeAsync()
    {
        _ = await InvokeAsync("allow", "53");
        string[] reports = ["raw", "builtins", "before-rules", "user-rules", "after-rules", "logging-rules", "listening", "added"];
        foreach (string report in reports)
        {
            CommandResult result = await InvokeAsync("show", report);
            Assert.AreEqual(0, result.ExitCode, report);
            Assert.IsGreaterThan(0, result.StdOut.Length, report);
        }

        CommandResult added = await InvokeAsync("show", "added");
        Assert.Contains("Added user rules", added.StdOut);
        Assert.Contains("ufw allow", added.StdOut);
    }

    [TestMethod]
    public async Task ExplicitRuleKeywordAndExtendedProtocolsAreAcceptedAsync()
    {
        _ = await InvokeAsync("rule", "allow", "proto", "gre", "to", "10.0.0.1");
        _ = await InvokeAsync("route", "deny", "proto", "udp", "from", "10.0.0.0/8", "to", "192.168.0.0/16", "port", "514");
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        Assert.Contains("10.0.0.1", status.StdOut);
        Assert.Contains("DENY FWD", status.StdOut);
        Assert.Contains("514/udp", status.StdOut);
    }

    [TestMethod]
    public async Task NumberedStatusGroupsIpv4RulesBeforeIpv6RulesAsync()
    {
        _ = await InvokeAsync("allow", "22/tcp");
        _ = await InvokeAsync("allow", "80/tcp");
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        int sshV4 = status.StdOut.IndexOf("[ 1] 22/tcp", StringComparison.Ordinal);
        int httpV4 = status.StdOut.IndexOf("[ 2] 80/tcp", StringComparison.Ordinal);
        int sshV6 = status.StdOut.IndexOf("[ 3] 22/tcp (v6)", StringComparison.Ordinal);
        int httpV6 = status.StdOut.IndexOf("[ 4] 80/tcp (v6)", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, sshV4);
        Assert.IsGreaterThan(sshV4, httpV4);
        Assert.IsGreaterThan(httpV4, sshV6);
        Assert.IsGreaterThan(sshV6, httpV6);
    }

    [TestMethod]
    public async Task RouteDeleteByNumberMatchesUfwRejectionAsync()
    {
        _ = await InvokeAsync("route", "allow", "in", "on", "eth0", "out", "on", "eth1");

        CommandResult delete = await InvokeAsync("--force", "route", "delete", "1");
        Assert.AreEqual(1, delete.ExitCode);
        Assert.Contains("'route delete NUM' unsupported", delete.StdErr);

        _ = await InvokeAsync("--force", "enable");
        CommandResult status = await InvokeAsync("status", "numbered");
        Assert.Contains("ALLOW FWD", status.StdOut);
    }

    [TestMethod]
    public async Task RoutedDirectionRequiresAnInterfaceAsync()
    {
        CommandResult incoming = await InvokeAsync("route", "allow", "in", "to", "10.0.0.1");
        Assert.AreEqual(1, incoming.ExitCode);
        Assert.Contains("Invalid interface clause for route rule", incoming.StdErr);

        CommandResult outgoing = await InvokeAsync("route", "allow", "out", "to", "10.0.0.1");
        Assert.AreEqual(1, outgoing.ExitCode);
        Assert.Contains("Invalid interface clause for route rule", outgoing.StdErr);
    }

    [TestMethod]
    public async Task Ipv4OnlyProtocolsRejectIpv6RulesAsync()
    {
        CommandResult invalid = await InvokeAsync("allow", "proto", "igmp", "from", "2001:db8::1", "to", "any");
        Assert.AreEqual(1, invalid.ExitCode);
        Assert.Contains("Invalid IPv6 address with protocol 'igmp'", invalid.StdErr);

        CommandResult valid = await InvokeAsync("allow", "proto", "igmp", "to", "224.0.0.1");
        Assert.AreEqual(0, valid.ExitCode);
    }

    [TestMethod]
    public async Task SourceAndDestinationApplicationProfilesRemainDistinctAsync()
    {
        await File.WriteAllTextAsync(_statePath, """
            {
              "schemaVersion": 1,
              "applicationProfiles": [
                {
                  "name": "Client",
                  "title": "Client ports",
                  "description": "Client profile",
                  "ports": "12345/tcp"
                },
                {
                  "name": "Server",
                  "title": "Server ports",
                  "description": "Server profile",
                  "ports": "443/tcp"
                }
              ]
            }
            """, TestContext.CancellationToken);

        CommandResult add = await InvokeAsync("allow", "from", "any", "app", "Client", "to", "any", "app", "Server");
        Assert.AreEqual(0, add.ExitCode);
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        Assert.Contains("Server", status.StdOut);
        Assert.Contains("Client", status.StdOut);
        Assert.IsLessThan(status.StdOut.IndexOf("Client", StringComparison.Ordinal), status.StdOut.IndexOf("Server", StringComparison.Ordinal));

        CommandResult added = await InvokeAsync("show", "added");
        Assert.Contains("from 0.0.0.0/0 app Client to 0.0.0.0/0 app Server", added.StdOut);
    }

    [TestMethod]
    public async Task RuleParserRejectsMissingRuleBodyAndInvalidInterfacesAsync()
    {
        CommandResult missing = await InvokeAsync("allow");
        Assert.AreEqual(1, missing.ExitCode);
        Assert.Contains("Not enough arguments", missing.StdErr);

        CommandResult invalidInterface = await InvokeAsync("allow", "in", "on", "interface-name-too-long", "22/tcp");
        Assert.AreEqual(1, invalidInterface.ExitCode);
        Assert.Contains("Invalid interface", invalidInterface.StdErr);
    }

    [TestMethod]
    public async Task ZeroArgumentCommandsRejectSurplusArgumentsAsync()
    {
        CommandResult enable = await InvokeAsync("enable", "unexpected");
        Assert.AreEqual(1, enable.ExitCode);
        Assert.Contains("Usage: ufw enable", enable.StdErr);

        CommandResult appList = await InvokeAsync("app", "list", "unexpected");
        Assert.AreEqual(1, appList.ExitCode);
        Assert.Contains("Usage: ufw app list", appList.StdErr);
    }

    [TestMethod]
    public async Task DefaultPolicyAcceptsUfwDirectionAliasesAsync()
    {
        _ = await InvokeAsync("default", "allow", "input");
        _ = await InvokeAsync("default", "deny", "output");
        _ = await InvokeAsync("default", "reject", "forward");
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "verbose");
        Assert.Contains("Default: allow (incoming), deny (outgoing), reject (routed)", status.StdOut);
    }

    [TestMethod]
    public async Task PerRuleLoggingMustAppearBeforeTheRuleBodyAsync()
    {
        CommandResult simple = await InvokeAsync("allow", "log", "22/tcp");
        Assert.AreEqual(0, simple.ExitCode);

        CommandResult interfaceRule = await InvokeAsync("allow", "in", "on", "eth0", "log-all", "to", "10.0.0.1", "port", "443", "proto", "tcp");
        Assert.AreEqual(0, interfaceRule.ExitCode);

        CommandResult invalid = await InvokeAsync("allow", "22/tcp", "log");
        Assert.AreEqual(1, invalid.ExitCode);
        Assert.Contains("Option 'log' not allowed here", invalid.StdErr);
    }

    [TestMethod]
    public async Task NamedServicesResolveDeterministicallyAndBeatProfileNameCollisionsAsync()
    {
        await File.WriteAllTextAsync(_statePath, """
            {
              "schemaVersion": 1,
              "applicationProfiles": [
                {
                  "name": "http",
                  "title": "Collision",
                  "description": "Should not shadow /etc/services-compatible names",
                  "ports": "8080/tcp"
                }
              ]
            }
            """, TestContext.CancellationToken);

        CommandResult simple = await InvokeAsync("allow", "http");
        Assert.AreEqual(0, simple.ExitCode);
        CommandResult full = await InvokeAsync("allow", "from", "any", "to", "any", "port", "https");
        Assert.AreEqual(0, full.ExitCode);
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        Assert.Contains("80/tcp", status.StdOut);
        Assert.Contains("443/tcp", status.StdOut);
        Assert.IsFalse(status.StdOut.Contains("8080", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task InvalidPersistedStateFailsAsACliErrorAsync()
    {
        await File.WriteAllTextAsync(_statePath, """
            {
              "schemaVersion": 1,
              "loggingLevel": "surprise"
            }
            """, TestContext.CancellationToken);

        CommandResult status = await InvokeAsync("status");
        Assert.AreEqual(1, status.ExitCode);
        Assert.Contains("Invalid mock state", status.StdErr);
        Assert.Contains("logging level", status.StdErr);
    }

    [TestMethod]
    public async Task UnknownRootCommandFailsInsteadOfFallingBackToHelpAsync()
    {
        CommandResult result = await InvokeAsync("not-a-ufw-command");
        Assert.AreEqual(1, result.ExitCode);
        Assert.Contains("ERROR: Invalid syntax", result.StdErr);
    }

    [TestMethod]
    public async Task VersionIdentifiesUfwCompatibilityTargetAsync()
    {
        CommandResult version = await InvokeAsync("--version");
        Assert.AreEqual(0, version.ExitCode);
        Assert.Contains("ufw 0.36.2", version.StdOut);
        Assert.Contains("Ufw.Mock", version.StdOut);
    }

    private async Task<CommandResult> InvokeAsync(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        string? originalStatePath = Environment.GetEnvironmentVariable("UFW_MOCK_STATE_PATH");
        using StringWriter stdout = new(CultureInfo.InvariantCulture);
        using StringWriter stderr = new(CultureInfo.InvariantCulture);
        try
        {
            Environment.SetEnvironmentVariable("UFW_MOCK_STATE_PATH", _statePath);
            Console.SetOut(stdout);
            Console.SetError(stderr);
            int exitCode = await UfwMockApplication.RunAsync(args);
            return new CommandResult(exitCode, Normalize(stdout.ToString()), Normalize(stderr.ToString()));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Environment.SetEnvironmentVariable("UFW_MOCK_STATE_PATH", originalStatePath);
        }
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }
        return count;
    }

    private sealed record CommandResult(int ExitCode, string StdOut, string StdErr);
}
