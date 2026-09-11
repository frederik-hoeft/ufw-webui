using Ufw.Mock.Rules;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Services;

internal sealed class UfwRuleMutationCoordinator(UfwGlobalOptions options, UfwStateStore store, UfwRuleParser parser, UfwRuleMutationService mutations)
{
    public UfwRuleMutationOutcome<IReadOnlyList<UfwRuleMutationResult>> Add(FirewallAction action, IReadOnlyList<string> arguments, bool routed) =>
        Apply(state => mutations.Add(state, parser.Parse(action, arguments, routed, state)));

    public UfwRuleMutationOutcome<IReadOnlyList<UfwRuleMutationResult>> Insert(FirewallAction action, IReadOnlyList<string> arguments, bool routed, int insertNumber) =>
        Apply(state => mutations.Insert(state, parser.Parse(action, arguments, routed, state), insertNumber));

    public UfwRuleMutationOutcome<IReadOnlyList<UfwRuleMutationResult>> Prepend(FirewallAction action, IReadOnlyList<string> arguments, bool routed) =>
        Apply(state => mutations.Prepend(state, parser.Parse(action, arguments, routed, state)));

    public UfwRuleMutationOutcome<List<UfwMockRule>> Delete(FirewallAction action, IReadOnlyList<string> arguments, bool routed) =>
        Apply(state => mutations.Delete(state, parser.Parse(action, arguments, routed, state)));

    public UfwRuleMutationOutcome<UfwMockRule> DeleteByNumber(int displayNumber) =>
        Apply(state => mutations.DeleteByNumber(state, displayNumber));

    private UfwRuleMutationOutcome<T> Apply<T>(Func<UfwMockState, T> mutation) => store.Update(options.DryRun, state =>
    {
        T result = mutation(state);
        return new UfwRuleMutationOutcome<T>(result, state.Enabled);
    });
}
