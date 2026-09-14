using Ufw.Mock.Rules;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Services;

internal sealed class UfwRuleMutationCoordinator(
    UfwGlobalOptions options,
    UfwStateStore store,
    UfwRuleParser parser,
    UfwRuleMutationService mutations,
    UfwCompatibilityConfiguration compatibilityConfiguration)
{
    public UfwRuleMutationOutcome<IReadOnlyList<UfwRuleMutationResult>> Add(FirewallAction action, IReadOnlyList<string> arguments, bool routed) =>
        Apply((state, ipv6Enabled) => mutations.Add(state, parser.Parse(action, arguments, routed, state), ipv6Enabled));

    public UfwRuleMutationOutcome<IReadOnlyList<UfwRuleMutationResult>> Insert(FirewallAction action, IReadOnlyList<string> arguments, bool routed, int insertNumber) =>
        Apply((state, ipv6Enabled) => mutations.Insert(state, parser.Parse(action, arguments, routed, state), insertNumber, ipv6Enabled));

    public UfwRuleMutationOutcome<IReadOnlyList<UfwRuleMutationResult>> Prepend(FirewallAction action, IReadOnlyList<string> arguments, bool routed) =>
        Apply((state, ipv6Enabled) => mutations.Prepend(state, parser.Parse(action, arguments, routed, state), ipv6Enabled));

    public UfwRuleMutationOutcome<List<UfwMockRule>> Delete(FirewallAction action, IReadOnlyList<string> arguments, bool routed) =>
        Apply((state, ipv6Enabled) => mutations.Delete(state, parser.Parse(action, arguments, routed, state), ipv6Enabled));

    public UfwRuleMutationOutcome<UfwMockRule> DeleteByNumber(int displayNumber) =>
        Apply((state, ipv6Enabled) => mutations.DeleteByNumber(state, displayNumber, ipv6Enabled));

    private UfwRuleMutationOutcome<T> Apply<T>(Func<UfwMockState, bool, T> mutation) => store.Update(options.DryRun, state =>
    {
        bool ipv6Enabled = compatibilityConfiguration.IsIPv6Enabled(state);
        T result = mutation(state, ipv6Enabled);
        return new UfwRuleMutationOutcome<T>(result, state.Enabled);
    });
}
