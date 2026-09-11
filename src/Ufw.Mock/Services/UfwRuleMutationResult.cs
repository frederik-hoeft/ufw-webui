using Ufw.Mock.State;

namespace Ufw.Mock.Services;

internal sealed record UfwRuleMutationResult(UfwMockRule Rule, UfwRuleMutationKind Kind);
