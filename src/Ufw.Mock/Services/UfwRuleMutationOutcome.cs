namespace Ufw.Mock.Services;

internal sealed record UfwRuleMutationOutcome<T>(T Result, bool FirewallEnabled);
