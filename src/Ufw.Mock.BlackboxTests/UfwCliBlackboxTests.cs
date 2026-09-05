using System.Globalization;
using Ufw.Mock;

namespace Ufw.Mock.BlackboxTests;

[TestClass]
[DoNotParallelize]
public sealed class UfwCliBlackboxTests
{
    private string _temporaryDirectory = null!;
    private string _statePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "Ufw.Mock.BlackboxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
        _statePath = Path.Combine(_temporaryDirectory, "state.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
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
        StringAssert.Contains(enable.StdOut, "Firewall is active and enabled on system startup");

        CommandResult verbose = await InvokeAsync("status", "verbose");
        StringAssert.Contains(verbose.StdOut, "Status: active");
        StringAssert.Contains(verbose.StdOut, "Logging: on (low)");
        StringAssert.Contains(verbose.StdOut, "Default: deny (incoming), allow (outgoing), deny (routed)");
        StringAssert.Contains(verbose.StdOut, "New profiles: skip");
    }

    [TestMethod]
    public async Task SystemdStyleRuleArgumentsMaterializeIpv4AndIpv6RowsAsync()
    {
        CommandResult add = await InvokeAsync(
            "--force",
            "allow",
            "in",
            "from",
            "any",
            "to",
            "any",
            "port",
            "22",
            "proto",
            "tcp",
            "comment",
            "ssh");
        Assert.AreEqual(0, add.ExitCode);
        Assert.AreEqual("Rules updated\nRules updated (v6)", add.StdOut);

        _ = await InvokeAsync("--force", "enable");
        CommandResult status = await InvokeAsync("status", "numbered");
        StringAssert.Contains(status.StdOut, "[ 1] 22/tcp");
        StringAssert.Contains(status.StdOut, "ALLOW IN");
        StringAssert.Contains(status.StdOut, "Anywhere # ssh");
        StringAssert.Contains(status.StdOut, "[ 2] 22/tcp (v6)");
        StringAssert.Contains(status.StdOut, "Anywhere (v6) # ssh");
        Assert.IsFalse(status.StdOut.Contains("Anywhere/tcp", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ExplicitZeroNetworksRemainFamilySpecificAsync()
    {
        _ = await InvokeAsync(
            "allow",
            "from",
            "0.0.0.0/0",
            "to",
            "0.0.0.0/0",
            "port",
            "80",
            "proto",
            "tcp");
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
        StringAssert.Contains(status.StdOut, "[ 1] 80/tcp");
        StringAssert.Contains(status.StdOut, "[ 2] 443/tcp (v6)");
        Assert.AreEqual(1, CountOccurrences(status.StdOut, "80/tcp"));
        Assert.AreEqual(1, CountOccurrences(status.StdOut, "443/tcp"));
    }

    [TestMethod]
    public async Task FullAndRoutedSyntaxProducesUfwStatusShapeAsync()
    {
        _ = await InvokeAsync("--force", "enable");
        CommandResult add = await InvokeAsync(
            "route",
            "allow",
            "in",
            "on",
            "eth0",
            "out",
            "on",
            "eth1",
            "to",
            "10.0.0.0/8",
            "from",
            "192.168.0.0/16");
        Assert.AreEqual(0, add.ExitCode);
        Assert.AreEqual("Rule added", add.StdOut);

        CommandResult status = await InvokeAsync("status", "numbered");
        StringAssert.Contains(status.StdOut, "10.0.0.0/8 on eth1");
        StringAssert.Contains(status.StdOut, "ALLOW FWD");
        StringAssert.Contains(status.StdOut, "192.168.0.0/16 on eth0");
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
        StringAssert.Contains(afterNumber.StdOut, "22/tcp (v6)");

        _ = await InvokeAsync("--force", "reset");
        _ = await InvokeAsync("allow", "22/tcp");
        _ = await InvokeAsync("--force", "enable");
        CommandResult syntaxDelete = await InvokeAsync("delete", "allow", "22/tcp");
        Assert.AreEqual(0, syntaxDelete.ExitCode);
        StringAssert.Contains(syntaxDelete.StdOut, "Rule deleted");
        StringAssert.Contains(syntaxDelete.StdOut, "Rule deleted (v6)");
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
        Assert.IsTrue(reject < http);
        Assert.IsTrue(http < ssh);
        Assert.IsTrue(ssh < https);
    }

    [TestMethod]
    public async Task InsertUsesGlobalNumberingForExplicitIpv6RulesAsync()
    {
        _ = await InvokeAsync("allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "80", "proto", "tcp");
        _ = await InvokeAsync("allow", "from", "::/0", "to", "::/0", "port", "80", "proto", "tcp");
        _ = await InvokeAsync("allow", "from", "::/0", "to", "::/0", "port", "443", "proto", "tcp");

        CommandResult inserted = await InvokeAsync(
            "insert",
            "3",
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

        CommandResult wrongFamily = await InvokeAsync(
            "insert",
            "1",
            "deny",
            "from",
            "::/0",
            "to",
            "::/0",
            "port",
            "25",
            "proto",
            "tcp");
        Assert.AreEqual(1, wrongFamily.ExitCode);
        StringAssert.Contains(wrongFamily.StdErr, "Invalid position '1'");

        _ = await InvokeAsync("--force", "enable");
        CommandResult status = await InvokeAsync("status", "numbered");
        int httpV6 = status.StdOut.IndexOf("80/tcp (v6)", StringComparison.Ordinal);
        int sshV6 = status.StdOut.IndexOf("22/tcp (v6)", StringComparison.Ordinal);
        int httpsV6 = status.StdOut.IndexOf("443/tcp (v6)", StringComparison.Ordinal);
        Assert.IsTrue(httpV6 >= 0);
        Assert.IsTrue(sshV6 > httpV6);
        Assert.IsTrue(httpsV6 > sshV6);
    }

    [TestMethod]
    public async Task FamilyNeutralInsertAlignsWithCounterpartAcrossAddressFamiliesAsync()
    {
        _ = await InvokeAsync("allow", "80/tcp");
        _ = await InvokeAsync("allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "443", "proto", "tcp");

        CommandResult inserted = await InvokeAsync("insert", "3", "deny", "22/tcp");
        Assert.AreEqual(0, inserted.ExitCode);
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        int sshV4 = status.StdOut.IndexOf("22/tcp", StringComparison.Ordinal);
        int httpV4 = status.StdOut.IndexOf("80/tcp", StringComparison.Ordinal);
        int httpsV4 = status.StdOut.IndexOf("443/tcp", StringComparison.Ordinal);
        int sshV6 = status.StdOut.IndexOf("22/tcp (v6)", StringComparison.Ordinal);
        int httpV6 = status.StdOut.IndexOf("80/tcp (v6)", StringComparison.Ordinal);
        Assert.IsTrue(sshV4 >= 0);
        Assert.IsTrue(httpV4 > sshV4);
        Assert.IsTrue(httpsV4 > httpV4);
        Assert.IsTrue(sshV6 > httpsV4);
        Assert.IsTrue(httpV6 > sshV6);
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
        StringAssert.Contains(dryRun.StdOut, "Firewall is active");

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
        StringAssert.Contains(status.StdOut, "Logging: on (full)");
        StringAssert.Contains(status.StdOut, "Default: reject (incoming), deny (outgoing), allow (routed)");

        _ = await InvokeAsync("logging", "off");
        CommandResult loggingOff = await InvokeAsync("status", "verbose");
        StringAssert.Contains(loggingOff.StdOut, "Logging: off");
    }

    [TestMethod]
    public async Task InvalidRuleFailsWithoutPersistingItAsync()
    {
        CommandResult invalid = await InvokeAsync("allow", "proto", "gre", "to", "any", "port", "80");
        Assert.AreEqual(1, invalid.ExitCode);
        StringAssert.Contains(invalid.StdErr, "ERROR:");
        StringAssert.Contains(invalid.StdErr, "cannot be combined with a port clause");

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
            """);

        CommandResult list = await InvokeAsync("app", "list");
        StringAssert.Contains(list.StdOut, "Available applications:");
        StringAssert.Contains(list.StdOut, "OpenSSH");

        CommandResult info = await InvokeAsync("app", "info", "OpenSSH");
        StringAssert.Contains(info.StdOut, "Profile: OpenSSH");
        StringAssert.Contains(info.StdOut, "Port:\n  22/tcp");

        _ = await InvokeAsync("app", "default", "allow");
        CommandResult update = await InvokeAsync("app", "update", "--add-new", "OpenSSH");
        Assert.AreEqual(0, update.ExitCode);
        StringAssert.Contains(update.StdOut, "Rules updated for profile 'OpenSSH'");
        _ = await InvokeAsync("--force", "enable");
        CommandResult status = await InvokeAsync("status", "numbered");
        StringAssert.Contains(status.StdOut, "OpenSSH");
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
            """);

        CommandResult result = await InvokeAsync("app", "update", "--add-new", "all");
        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.StdErr, "Cannot specify 'all' with '--add-new'");
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
        StringAssert.Contains(added.StdOut, "Added user rules");
        StringAssert.Contains(added.StdOut, "ufw allow");
    }

    [TestMethod]
    public async Task ExplicitRuleKeywordAndExtendedProtocolsAreAcceptedAsync()
    {
        _ = await InvokeAsync("rule", "allow", "proto", "gre", "to", "10.0.0.1");
        _ = await InvokeAsync("route", "deny", "proto", "udp", "from", "10.0.0.0/8", "to", "192.168.0.0/16", "port", "514");
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        StringAssert.Contains(status.StdOut, "10.0.0.1");
        StringAssert.Contains(status.StdOut, "DENY FWD");
        StringAssert.Contains(status.StdOut, "514/udp");
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
        Assert.IsTrue(sshV4 >= 0);
        Assert.IsTrue(httpV4 > sshV4);
        Assert.IsTrue(sshV6 > httpV4);
        Assert.IsTrue(httpV6 > sshV6);
    }

    [TestMethod]
    public async Task RouteDeleteByNumberMatchesUfwRejectionAsync()
    {
        _ = await InvokeAsync("route", "allow", "in", "on", "eth0", "out", "on", "eth1");

        CommandResult delete = await InvokeAsync("--force", "route", "delete", "1");
        Assert.AreEqual(1, delete.ExitCode);
        StringAssert.Contains(delete.StdErr, "'route delete NUM' unsupported");

        _ = await InvokeAsync("--force", "enable");
        CommandResult status = await InvokeAsync("status", "numbered");
        StringAssert.Contains(status.StdOut, "ALLOW FWD");
    }

    [TestMethod]
    public async Task RoutedDirectionRequiresAnInterfaceAsync()
    {
        CommandResult incoming = await InvokeAsync("route", "allow", "in", "to", "10.0.0.1");
        Assert.AreEqual(1, incoming.ExitCode);
        StringAssert.Contains(incoming.StdErr, "Invalid interface clause for route rule");

        CommandResult outgoing = await InvokeAsync("route", "allow", "out", "to", "10.0.0.1");
        Assert.AreEqual(1, outgoing.ExitCode);
        StringAssert.Contains(outgoing.StdErr, "Invalid interface clause for route rule");
    }

    [TestMethod]
    public async Task Ipv4OnlyProtocolsRejectIpv6RulesAsync()
    {
        CommandResult invalid = await InvokeAsync("allow", "proto", "igmp", "from", "2001:db8::1", "to", "any");
        Assert.AreEqual(1, invalid.ExitCode);
        StringAssert.Contains(invalid.StdErr, "Invalid IPv6 address with protocol 'igmp'");

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
            """);

        CommandResult add = await InvokeAsync("allow", "from", "any", "app", "Client", "to", "any", "app", "Server");
        Assert.AreEqual(0, add.ExitCode);
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        StringAssert.Contains(status.StdOut, "Server");
        StringAssert.Contains(status.StdOut, "Client");
        Assert.IsTrue(status.StdOut.IndexOf("Server", StringComparison.Ordinal) < status.StdOut.IndexOf("Client", StringComparison.Ordinal));

        CommandResult added = await InvokeAsync("show", "added");
        StringAssert.Contains(added.StdOut, "from 0.0.0.0/0 app Client to 0.0.0.0/0 app Server");
    }

    [TestMethod]
    public async Task RuleParserRejectsMissingRuleBodyAndInvalidInterfacesAsync()
    {
        CommandResult missing = await InvokeAsync("allow");
        Assert.AreEqual(1, missing.ExitCode);
        StringAssert.Contains(missing.StdErr, "Not enough arguments");

        CommandResult invalidInterface = await InvokeAsync("allow", "in", "on", "interface-name-too-long", "22/tcp");
        Assert.AreEqual(1, invalidInterface.ExitCode);
        StringAssert.Contains(invalidInterface.StdErr, "Invalid interface");
    }

    [TestMethod]
    public async Task ZeroArgumentCommandsRejectSurplusArgumentsAsync()
    {
        CommandResult enable = await InvokeAsync("enable", "unexpected");
        Assert.AreEqual(1, enable.ExitCode);
        StringAssert.Contains(enable.StdErr, "Usage: ufw enable");

        CommandResult appList = await InvokeAsync("app", "list", "unexpected");
        Assert.AreEqual(1, appList.ExitCode);
        StringAssert.Contains(appList.StdErr, "Usage: ufw app list");
    }

    [TestMethod]
    public async Task DefaultPolicyAcceptsUfwDirectionAliasesAsync()
    {
        _ = await InvokeAsync("default", "allow", "input");
        _ = await InvokeAsync("default", "deny", "output");
        _ = await InvokeAsync("default", "reject", "forward");
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "verbose");
        StringAssert.Contains(status.StdOut, "Default: allow (incoming), deny (outgoing), reject (routed)");
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
        StringAssert.Contains(invalid.StdErr, "Option 'log' not allowed here");
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
            """);

        CommandResult simple = await InvokeAsync("allow", "http");
        Assert.AreEqual(0, simple.ExitCode);
        CommandResult full = await InvokeAsync("allow", "from", "any", "to", "any", "port", "https");
        Assert.AreEqual(0, full.ExitCode);
        _ = await InvokeAsync("--force", "enable");

        CommandResult status = await InvokeAsync("status", "numbered");
        StringAssert.Contains(status.StdOut, "80/tcp");
        StringAssert.Contains(status.StdOut, "443/tcp");
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
            """);

        CommandResult status = await InvokeAsync("status");
        Assert.AreEqual(1, status.ExitCode);
        StringAssert.Contains(status.StdErr, "Invalid mock state");
        StringAssert.Contains(status.StdErr, "logging level");
    }

    [TestMethod]
    public async Task UnknownRootCommandFailsInsteadOfFallingBackToHelpAsync()
    {
        CommandResult result = await InvokeAsync("not-a-ufw-command");
        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.StdErr, "ERROR: Invalid syntax");
    }

    [TestMethod]
    public async Task VersionIdentifiesUfwCompatibilityTargetAsync()
    {
        CommandResult version = await InvokeAsync("--version");
        Assert.AreEqual(0, version.ExitCode);
        StringAssert.Contains(version.StdOut, "ufw 0.36.2");
        StringAssert.Contains(version.StdOut, "Ufw.Mock");
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
