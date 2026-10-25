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
        if (!configuration.Settings.FilterSystemStateEnabled)
        {
            return new FilterSystemStateReadResult.Disabled();
        }

        Observation<RawFilterTable> ipv4 = await ReadSaveAsync(configuration.Settings.IptablesSavePath, FirewallAddressFamily.IPv4, "IPv4", cancellationToken);
        if (ipv4.Diagnostic is not null)
        {
            return new FilterSystemStateReadResult.Failure(ipv4.Diagnostic);
        }

        Observation<RawFilterTable> ipv6 = await ReadSaveAsync(configuration.Settings.Ip6tablesSavePath, FirewallAddressFamily.IPv6, "IPv6", cancellationToken);
        if (ipv6.Diagnostic is not null)
        {
            return new FilterSystemStateReadResult.Failure(ipv6.Diagnostic);
        }

        Observation<IReadOnlyList<UfwUserRuleTranslation>> ipv4Rules = ReadUserRules(configuration.Settings.UfwUserRulesPath, FirewallAddressFamily.IPv4, "IPv4");
        Observation<IReadOnlyList<UfwUserRuleTranslation>> ipv6Rules = ReadUserRules(configuration.Settings.UfwUser6RulesPath, FirewallAddressFamily.IPv6, "IPv6");
        FilterEnrichmentReport ipv4Enrichment = ipv4Rules.Diagnostic is null
            ? new FilterEnrichmentReport(FilterEnrichmentStatus.Complete)
            : new FilterEnrichmentReport(FilterEnrichmentStatus.Unavailable, ipv4Rules.Diagnostic);
        FilterEnrichmentReport ipv6Enrichment = ipv6Rules.Diagnostic is null
            ? new FilterEnrichmentReport(FilterEnrichmentStatus.Complete)
            : new FilterEnrichmentReport(FilterEnrichmentStatus.Unavailable, ipv6Rules.Diagnostic);

        // UFW observations and Netfilter saves are not atomic. Never use optional enrichment
        // to filter the authoritative physical inventory or to suppress a successful read.
        IReadOnlyList<UfwUserRuleTranslation> managedIpv4 = [];
        IReadOnlyList<UfwUserRuleTranslation> managedIpv6 = [];
        if (ipv4Rules.Diagnostic is null || ipv6Rules.Diagnostic is null)
        {
            FirewallRuleSnapshotReadResult managed = await snapshotReader.ReadAsync(cancellationToken);
            if (managed is FirewallRuleSnapshotReadResult.Success success)
            {
                if (ipv4Rules.Diagnostic is null)
                {
                    managedIpv4 = UfwManagedRuleLinker.Link(ipv4Rules.Value!, success.Snapshot.Rules, FirewallAddressFamily.IPv4);
                    if (managedIpv4.Count != ipv4Rules.Value!.Count)
                    {
                        ipv4Enrichment = new FilterEnrichmentReport(FilterEnrichmentStatus.Partial, "Some IPv4 user-rule tuples do not match the numbered UFW snapshot.");
                    }
                }

                if (ipv6Rules.Diagnostic is null)
                {
                    managedIpv6 = UfwManagedRuleLinker.Link(ipv6Rules.Value!, success.Snapshot.Rules, FirewallAddressFamily.IPv6);
                    if (managedIpv6.Count != ipv6Rules.Value!.Count)
                    {
                        ipv6Enrichment = new FilterEnrichmentReport(FilterEnrichmentStatus.Partial, "Some IPv6 user-rule tuples do not match the numbered UFW snapshot.");
                    }
                }
            }
            else
            {
                const string DIAGNOSTIC = "The numbered UFW snapshot is unavailable; managed-rule attribution was skipped.";
                _logger.LogWarning(DIAGNOSTIC);
                if (ipv4Rules.Diagnostic is null)
                {
                    ipv4Enrichment = new FilterEnrichmentReport(FilterEnrichmentStatus.Unavailable, DIAGNOSTIC);
                }

                if (ipv6Rules.Diagnostic is null)
                {
                    ipv6Enrichment = new FilterEnrichmentReport(FilterEnrichmentStatus.Unavailable, DIAGNOSTIC);
                }
            }
        }

        return new FilterSystemStateReadResult.Success(FilterSystemStateFactory.Create(
            ipv4.Value!, ipv6.Value!, managedIpv4, managedIpv6, ipv4Enrichment, ipv6Enrichment));
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
            return Observation<IReadOnlyList<UfwUserRuleTranslation>>.Failed($"The {familyName} UFW user-rule file is unavailable.");
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
