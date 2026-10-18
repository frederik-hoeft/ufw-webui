using System.Collections.Immutable;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Interop.Iptables.Grammars;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall.SystemState;

internal sealed class FilterSystemStateReader(IConfiguration configuration, IChildProcessRunner processes, IFirewallRuleSnapshotReader snapshotReader, ILogger logger) : IFilterSystemStateReader
{
    private static readonly ImmutableDictionary<string, string> s_environment = ImmutableDictionary<string, string>.Empty
        .Add("LC_ALL", "C")
        .Add("LANG", "C");

    private static readonly ImmutableArray<string> s_saveArguments = ["-t", "filter"];

    private readonly ILogger<FilterSystemStateReader> _logger = logger.Scoped<FilterSystemStateReader>();

    public async ValueTask<FilterSystemStateReadResult> ReadAsync(CancellationToken cancellationToken)
    {
        Observation<RawFilterTable> ipv4 = await ReadSaveAsync(configuration.Settings.IptablesSavePath, FirewallAddressFamily.IPv4, "IPv4", cancellationToken);
        if (ipv4.Diagnostic is not null)
        {
            return new FilterSystemStateReadResult.Failure(ipv4.Diagnostic);
        }

        Observation<RawFilterTable> ipv6 = await ReadSaveAsync(configuration.Settings.Ip6TablesSavePath, FirewallAddressFamily.IPv6, "IPv6", cancellationToken);
        if (ipv6.Diagnostic is not null)
        {
            return new FilterSystemStateReadResult.Failure(ipv6.Diagnostic);
        }

        Observation<IReadOnlyList<UfwUserRuleTranslation>> ipv4Rules = ReadUserRules(configuration.Settings.UfwUserRulesPath, FirewallAddressFamily.IPv4, "IPv4");
        if (ipv4Rules.Diagnostic is not null)
        {
            return new FilterSystemStateReadResult.Failure(ipv4Rules.Diagnostic);
        }

        Observation<IReadOnlyList<UfwUserRuleTranslation>> ipv6Rules = ReadUserRules(configuration.Settings.UfwUser6RulesPath, FirewallAddressFamily.IPv6, "IPv6");
        if (ipv6Rules.Diagnostic is not null)
        {
            return new FilterSystemStateReadResult.Failure(ipv6Rules.Diagnostic);
        }

        // The two UFW and Netfilter observations are not atomic. Treat the numbered UFW
        // snapshot as the authority for managed identities, but never use it to filter
        // the physical inventory. If the observations disagree, omit only enrichment.
        FirewallRuleSnapshotReadResult managed = await snapshotReader.ReadAsync(cancellationToken);
        IReadOnlyList<UfwUserRuleTranslation> managedIpv4 = [];
        IReadOnlyList<UfwUserRuleTranslation> managedIpv6 = [];
        if (managed is FirewallRuleSnapshotReadResult.Success success)
        {
            managedIpv4 = UfwManagedRuleLinker.Link(ipv4Rules.Value!, success.Snapshot.Rules, FirewallAddressFamily.IPv4);
            managedIpv6 = UfwManagedRuleLinker.Link(ipv6Rules.Value!, success.Snapshot.Rules, FirewallAddressFamily.IPv6);
        }
        else
        {
            _logger.LogWarning("Could not reconcile the managed UFW snapshot; physical filter inventory remains available without managed attribution.");
        }

        return new FilterSystemStateReadResult.Success(FilterSystemStateFactory.Create(ipv4.Value!, ipv6.Value!, managedIpv4, managedIpv6));
    }

    private async Task<Observation<RawFilterTable>> ReadSaveAsync(
        string path,
        FirewallAddressFamily family,
        string familyName,
        CancellationToken cancellationToken)
    {
        ChildProcessResult result;
        try
        {
            result = await processes.RunAsync(new ChildProcessRequest(path, s_saveArguments, s_environment), cancellationToken);
        }
        catch (ChildProcessException exception)
        {
            _logger.LogError(exception, $"Failed to execute {familyName} filter save '{path}'.");
            return Observation<RawFilterTable>.Failed($"Failed to execute the {familyName} filter save command.");
        }

        if (result.CancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (result.ExitCode != 0)
        {
            _logger.LogError($"{familyName} filter save '{path}' exited {result.ExitCode}: {result.StandardError}");
            return Observation<RawFilterTable>.Failed($"The {familyName} filter save command failed.");
        }

        if (!IptablesSaveGrammar.Instance.TryParse(result.StandardOutput, family, out RawFilterTable? table))
        {
            _logger.LogError($"{familyName} filter save output could not be parsed.");
            return Observation<RawFilterTable>.Failed($"The {familyName} filter save output could not be parsed.");
        }

        return Observation<RawFilterTable>.Succeeded(table);
    }

    private Observation<IReadOnlyList<UfwUserRuleTranslation>> ReadUserRules(string path, FirewallAddressFamily family, string familyName)
    {
        if (!File.Exists(path))
        {
            return Observation<IReadOnlyList<UfwUserRuleTranslation>>.Succeeded([]);
        }

        string contents;
        try
        {
            contents = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, $"Failed to read {familyName} UFW user rules '{path}'.");
            return Observation<IReadOnlyList<UfwUserRuleTranslation>>.Failed($"Failed to read {familyName} UFW user rules.");
        }

        if (!UfwUserRulesGrammar.Instance.TryParse(contents, family, out IReadOnlyList<UfwUserRuleTranslation>? translations))
        {
            _logger.LogError($"{familyName} UFW user rules '{path}' could not be parsed.");
            return Observation<IReadOnlyList<UfwUserRuleTranslation>>.Failed($"The {familyName} UFW user rules file could not be parsed.");
        }

        return Observation<IReadOnlyList<UfwUserRuleTranslation>>.Succeeded(translations);
    }

    private readonly record struct Observation<T>(T? Value, string? Diagnostic)
        where T : class
    {
        public static Observation<T> Succeeded(T value) => new(value, null);

        public static Observation<T> Failed(string diagnostic) => new(null, diagnostic);
    }
}
