using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class RuleReorderRecoveryCoordinator(
    IFirewallRuleSnapshotReader snapshotReader,
    IUfwRunner ufwRunner,
    IUfwRuleCommandRenderer renderer,
    IReorderRecoveryJournal journal,
    ILogger logger) : IRuleReorderRecoveryCoordinator
{
    private readonly ILogger<RuleReorderRecoveryCoordinator> _logger = logger.Scoped<RuleReorderRecoveryCoordinator>();

    public async Task<RuleRecoveryResult> EnsurePresentAsync(
        ReorderRecoveryJournalEntry entry,
        RuleListResponse? observedSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        RuleListResponse? snapshot = observedSnapshot ?? await TryReadSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            return new RuleRecoveryResult(
                false,
                false,
                null,
                "The authoritative firewall state could not be read, so recovery cannot safely determine whether reinsertion is required.");
        }
        if (CountMatches(snapshot.Rules, entry.Rule) >= entry.ExpectedMultiplicity)
        {
            await journal.ClearAsync(CancellationToken.None);
            return new RuleRecoveryResult(true, false, snapshot, null);
        }

        IUfwCommand command = CreateRecoveryCommand(entry, snapshot);
        string? processDiagnostic = null;
        try
        {
            UfwProcessResult result = await ufwRunner.ExecuteAsync(command, cancellationToken);
            if (!result.Succeeded || result.CancellationRequested)
            {
                processDiagnostic = FormatProcessDiagnostic(result);
                _logger.LogWarning($"Reorder recovery insertion did not report success: {processDiagnostic}");
            }
        }
        catch (ChildProcessException exception)
        {
            processDiagnostic = exception.Message;
            _logger.LogError(exception, "Failed to start UFW while recovering an interrupted reorder move.");
        }

        RuleListResponse? confirmed = await TryReadSnapshotAsync(CancellationToken.None);
        if (confirmed is not null && CountMatches(confirmed.Rules, entry.Rule) >= entry.ExpectedMultiplicity)
        {
            await journal.ClearAsync(CancellationToken.None);
            return new RuleRecoveryResult(true, true, confirmed, processDiagnostic);
        }

        string diagnostic = processDiagnostic is null
            ? "The removed rule could not be confirmed after the recovery insertion."
            : $"{processDiagnostic} The removed rule could not be confirmed afterward.";
        return new RuleRecoveryResult(false, true, confirmed, diagnostic);
    }

    private IUfwCommand CreateRecoveryCommand(ReorderRecoveryJournalEntry entry, RuleListResponse snapshot)
    {
        int? nextIndex = FindUniqueAnchorIndex(snapshot.Rules, entry.NextAnchor);
        if (nextIndex.HasValue && snapshot.Rules[nextIndex.Value].DisplayNumber is int nextDisplayNumber)
        {
            return new UfwInsertRuleCommand(nextDisplayNumber, entry.Rule, renderer);
        }

        int? previousIndex = FindUniqueAnchorIndex(snapshot.Rules, entry.PreviousAnchor);
        if (previousIndex.HasValue && snapshot.Rules[previousIndex.Value].DisplayNumber is int previousDisplayNumber)
        {
            int insertionNumber = previousDisplayNumber + 1;
            if (insertionNumber <= snapshot.Rules.Count)
            {
                return new UfwInsertRuleCommand(insertionNumber, entry.Rule, renderer);
            }
        }

        if (entry.OriginalDisplayNumber <= snapshot.Rules.Count)
        {
            return new UfwInsertRuleCommand(entry.OriginalDisplayNumber, entry.Rule, renderer);
        }

        return new UfwAddRuleCommand(entry.Rule, renderer);
    }

    private async Task<RuleListResponse?> TryReadSnapshotAsync(CancellationToken cancellationToken)
    {
        FirewallRuleSnapshotReadResult read = await snapshotReader.ReadAsync(cancellationToken);
        return read.Error is null ? FirewallRuleSet.ToListResponse(read.Snapshot!) : null;
    }

    private static int? FindUniqueAnchorIndex(IReadOnlyList<ListedFirewallRule> rules, RuleRecoveryAnchor? anchor)
    {
        if (anchor is null)
        {
            return null;
        }

        int? match = null;
        for (int index = 0; index < rules.Count; index++)
        {
            if (!MatchesAnchor(rules[index], anchor))
            {
                continue;
            }
            if (match.HasValue)
            {
                return null;
            }
            match = index;
        }
        return match;
    }

    private static bool MatchesAnchor(ListedFirewallRule rule, RuleRecoveryAnchor anchor)
    {
        if (anchor.Rule is not null)
        {
            return rule.Rule is not null && FirewallRuleSemanticComparer.Equals(rule.Rule, anchor.Rule);
        }

        if (rule.Parsed || anchor.RawLine is null)
        {
            return false;
        }

        ListedFirewallRule synthetic = new()
        {
            Parsed = false,
            RawLine = anchor.RawLine,
        };
        return FirewallRuleSemanticComparer.Equals(rule, synthetic);
    }

    internal static int CountMatches(IReadOnlyList<ListedFirewallRule> rules, FirewallRuleSpecification specification) =>
        rules.Count(rule => rule.Rule is not null && FirewallRuleSemanticComparer.Equals(rule.Rule, specification));

    private static string FormatProcessDiagnostic(UfwProcessResult result)
    {
        string details = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        if (result.CancellationRequested)
        {
            return "UFW recovery was canceled after the process started.";
        }
        return $"UFW recovery exited with code {result.ExitCode}: {details}";
    }
}
