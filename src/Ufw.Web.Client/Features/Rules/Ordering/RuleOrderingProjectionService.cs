using Microsoft.Extensions.Localization;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Services.Localization;

namespace Ufw.Web.Client.Features.Rules.Ordering;

internal sealed class RuleOrderingProjectionService(IStringLocalizer<RulesStrings> rulesText) : IRuleOrderingProjectionService
{
    public RuleOrderingPreview Move(IReadOnlyList<ListedFirewallRule> authoritativeRules, RuleOrderingPreview? currentPreview, RuleMoveRequest request)
    {
        ArgumentNullException.ThrowIfNull(authoritativeRules);
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<int> currentOrder = currentPreview?.DesiredOrder ?? Enumerable.Range(0, authoritativeRules.Count).ToArray();
        if (currentOrder.Count != authoritativeRules.Count)
        {
            throw new InvalidOperationException(rulesText["OrderingBaselineChanged"]);
        }
        if (request.OccurrenceId < 0 || request.OccurrenceId >= authoritativeRules.Count)
        {
            throw new InvalidOperationException(rulesText["OrderingMissing"]);
        }

        FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(authoritativeRules[request.OccurrenceId]);
        if (request.AddressFamily != family)
        {
            throw new InvalidOperationException(rulesText["OrderingFamilyMismatch"]);
        }

        List<int> familyOrder = [.. currentOrder.Where(occurrenceId =>
            ListedFirewallRuleFamily.GetObservedFamily(authoritativeRules[occurrenceId]) == family)];
        if (request.TargetFamilyPosition < 1 || request.TargetFamilyPosition > familyOrder.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.TargetFamilyPosition,
                rulesText["OrderingTargetRange", familyOrder.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)]);
        }

        int sourceFamilyIndex = familyOrder.IndexOf(request.OccurrenceId);
        if (sourceFamilyIndex < 0)
        {
            throw new InvalidOperationException(rulesText["OrderingMissing"]);
        }

        familyOrder.RemoveAt(sourceFamilyIndex);
        familyOrder.Insert(request.TargetFamilyPosition - 1, request.OccurrenceId);

        int nextFamilyOccurrence = 0;
        int[] desiredOrder = [.. currentOrder.Select(occurrenceId =>
            ListedFirewallRuleFamily.GetObservedFamily(authoritativeRules[occurrenceId]) == family
                ? familyOrder[nextFamilyOccurrence++]
                : occurrenceId)];

        HashSet<int> directlyMovedOccurrences = currentPreview is null
            ? []
            : [.. currentPreview.DirectlyMovedOccurrences];
        directlyMovedOccurrences.Add(request.OccurrenceId);

        return new RuleOrderingPreview(desiredOrder, directlyMovedOccurrences);
    }
}
