using Ufw.Mock.Rules;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Services;

internal sealed class UfwRuleMutationReporter
{
    public void WriteResults(UfwRuleMutationOutcome<IReadOnlyList<UfwRuleMutationResult>> outcome)
    {
        foreach (UfwRuleMutationResult result in outcome.Result)
        {
            Console.WriteLine(FormatMutationResult(outcome.FirewallEnabled, result));
        }
    }

    public void WriteDeleted(UfwRuleMutationOutcome<List<UfwMockRule>> outcome)
    {
        foreach (UfwMockRule rule in outcome.Result)
        {
            Console.WriteLine(FormatMutationMessage(outcome.FirewallEnabled, "deleted", rule.Specification.AddressFamily));
        }
    }

    public void WriteDeleted(UfwRuleMutationOutcome<UfwMockRule> outcome) =>
        Console.WriteLine(FormatMutationMessage(outcome.FirewallEnabled, "deleted", outcome.Result.Specification.AddressFamily));

    private static string FormatMutationResult(bool enabled, UfwRuleMutationResult result)
    {
        string suffix = result.Rule.Specification.AddressFamily == FirewallAddressFamily.IPv6 ? " (v6)" : string.Empty;
        return result.Kind switch
        {
            UfwRuleMutationKind.Skipped => "Skipping adding existing rule" + suffix,
            UfwRuleMutationKind.Added => FormatMutationMessage(enabled, "added", result.Rule.Specification.AddressFamily),
            UfwRuleMutationKind.Inserted => enabled ? "Rule inserted" + suffix : "Rules updated" + suffix,
            UfwRuleMutationKind.Updated => FormatMutationMessage(enabled, "updated", result.Rule.Specification.AddressFamily),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Kind, null),
        };
    }

    private static string FormatMutationMessage(bool enabled, string operation, FirewallAddressFamily family)
    {
        string suffix = family == FirewallAddressFamily.IPv6 ? " (v6)" : string.Empty;
        return enabled ? $"Rule {operation}{suffix}" : "Rules updated" + suffix;
    }
}
