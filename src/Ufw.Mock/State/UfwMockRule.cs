using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Mock.State;

internal sealed class UfwMockRule
{
    public required FirewallRuleSpecification Specification { get; set; }

    public string? ExtendedProtocol { get; set; }

    public UfwRuleLogging Logging { get; set; }

    public string? SourceApplicationName { get; set; }

    public string? DestinationApplicationName { get; set; }
}
