using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class RuleReorderRecoveryCoordinator(
    IFirewallRuleSnapshotReader snapshotReader,
    IUfwProcessExecutor processExecutor,
    IUfwRuleCommandRenderer renderer,
    IReorderRecoveryJournal journal,
    ILogger logger) : IRuleReorderRecoveryCoordinator
{
    private readonly ILogger<RuleReorderRecoveryCoordinator> _logger = logger.Scoped<RuleReorderRecoveryCoordinator>();

    public async Task<RuleRecoveryResult> EnsurePresentAsync(ReorderRecoveryJournalEntry entry, RuleListResponse? observedSnapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        RuleListResponse? snapshot = observedSnapshot ?? await snapshotReader.ReadAsync(cancellationToken).OrDefaultAsync();
        if (snapshot is null)
        {
            return new RuleRecoveryResult(false, false, null, "The authoritative firewall state could not be read, so recovery cannot safely determine whether reinsertion is required.");
        }
        if (FirewallRuleSnapshotMatcher.CountMatches(snapshot.Rules, entry.Rule) >= entry.ExpectedMultiplicity)
        {
            await journal.ClearAsync(CancellationToken.None);
            return new RuleRecoveryResult(true, false, snapshot, null);
        }

        IUfwCommand command = CreateRecoveryCommand(entry, snapshot);
        UfwProcessExecutionResult process = await processExecutor.ExecuteAsync(command, "recovering an interrupted reorder move", cancellationToken);
        if (!process.Succeeded && !process.RunnerFailed)
        {
            _logger.LogWarning($"Reorder recovery insertion did not report success: {process.Diagnostic}");
        }

        RuleListResponse? confirmed = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        if (confirmed is not null && FirewallRuleSnapshotMatcher.CountMatches(confirmed.Rules, entry.Rule) >= entry.ExpectedMultiplicity)
        {
            await journal.ClearAsync(CancellationToken.None);
            return new RuleRecoveryResult(true, true, confirmed, process.Diagnostic);
        }

        string diagnostic = UfwProcessDiagnostics.Combine(process.Diagnostic, "The removed rule could not be confirmed after the recovery insertion.")!;
        return new RuleRecoveryResult(false, true, confirmed, diagnostic);
    }

    private IUfwCommand CreateRecoveryCommand(ReorderRecoveryJournalEntry entry, RuleListResponse snapshot)
    {
        int? nextIndex = FindUniqueAnchorIndex(snapshot.Rules, entry.NextAnchor);
        if (nextIndex.HasValue && snapshot.Rules[nextIndex.Value].Rule?.AddressFamily == entry.Rule.AddressFamily)
        {
            UfwInsertionPlacement placement = UfwInsertionPlacementResolver.Resolve(snapshot.Rules, entry.Rule.AddressFamily, nextIndex.Value);
            return placement.CreateCommand(entry.Rule, renderer);
        }

        int? previousIndex = FindUniqueAnchorIndex(snapshot.Rules, entry.PreviousAnchor);
        if (previousIndex.HasValue && snapshot.Rules[previousIndex.Value].Rule?.AddressFamily == entry.Rule.AddressFamily)
        {
            UfwInsertionPlacement placement = UfwInsertionPlacementResolver.Resolve(snapshot.Rules, entry.Rule.AddressFamily, previousIndex.Value + 1);
            return placement.CreateCommand(entry.Rule, renderer);
        }

        UfwInsertionPlacement fallback = UfwInsertionPlacementResolver.ResolveFamilyPosition(snapshot.Rules, entry.Rule.AddressFamily, entry.OriginalFamilyPosition);
        return fallback.CreateCommand(entry.Rule, renderer);
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
            return rule.Rule is not null && FirewallRuleStateComparer.Equals(rule.Rule, anchor.Rule);
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
        return FirewallRuleStateComparer.Equals(rule, synthetic);
    }
}
