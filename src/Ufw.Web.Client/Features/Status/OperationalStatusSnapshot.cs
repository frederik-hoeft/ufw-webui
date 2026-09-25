using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Features.Status;

internal enum OperationalAvailability
{
    Unknown,
    Available,
    Unavailable,
}

internal sealed record OperationalStatusSnapshot
{
    public static OperationalStatusSnapshot Unknown { get; } = new();

    public OperationalAvailability ManagementApi { get; init; } = OperationalAvailability.Unknown;

    public OperationalAvailability Daemon { get; init; } = OperationalAvailability.Unknown;

    public OperationalAvailability FirewallSnapshot { get; init; } = OperationalAvailability.Unknown;

    public bool? FirewallActive { get; init; }

    public int? RuleCount { get; init; }

    public int? IntentProtocolVersion { get; init; }

    public bool? IntentProtocolCompatible { get; init; }

    public string? DeploymentId { get; init; }

    public DateTimeOffset? CheckedAt { get; init; }

    public TimeSpan? RoundTrip { get; init; }

    public ClientError? ManagementApiError { get; init; }

    public ClientError? DaemonError { get; init; }

    public ClientError? IntentContextError { get; init; }

    public ClientError? FirewallError { get; init; }
}
