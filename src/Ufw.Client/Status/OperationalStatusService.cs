using Ufw.Client.Api;
using Ufw.Client.Errors;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Status;

internal sealed class OperationalStatusService
(
    IManagementApiHealthClient managementHealth,
    IDaemonStatusApiClient daemonStatus,
    IRuleApiClient rulesApiClient,
    IIntentContextApiClient intentContextApiClient,
    IClientErrorMapper clientErrors,
    TimeProvider timeProvider
) : IOperationalStatusService, IDisposable
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public OperationalStatusSnapshot Current { get; private set; } = OperationalStatusSnapshot.Unknown;

    public bool IsRefreshing { get; private set; }

    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            IsRefreshing = true;
            Changed?.Invoke();

            long startedAt = timeProvider.GetTimestamp();
            Task managementTask = managementHealth.ProbeAsync(cancellationToken);
            Task daemonTask = daemonStatus.ProbeAsync(cancellationToken);
            Task<IntentContextResponse> intentContextTask = intentContextApiClient.GetAsync(cancellationToken);
            Task<RuleInventoryResponse> rulesTask = rulesApiClient.GetInventoryAsync(cancellationToken);

            try
            {
                await Task.WhenAll(managementTask, daemonTask, intentContextTask, rulesTask);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Each probe is projected independently below so one failure cannot infer another component's state.
            }

            bool managementSucceeded = managementTask.Status == TaskStatus.RanToCompletion;
            bool daemonSucceeded = daemonTask.Status == TaskStatus.RanToCompletion;
            IntentContextResponse? intentContext = intentContextTask.Status == TaskStatus.RanToCompletion
                ? await intentContextTask
                : null;
            RuleListResponse? rules = rulesTask.Status == TaskStatus.RanToCompletion
                ? (await rulesTask).Firewall
                : null;

            OperationalAvailability managementAvailability = managementSucceeded
                ? OperationalAvailability.Available
                : OperationalAvailability.Unavailable;
            OperationalAvailability daemonAvailability = daemonSucceeded
                ? OperationalAvailability.Available
                : managementSucceeded
                    ? OperationalAvailability.Unavailable
                    : OperationalAvailability.Unknown;
            OperationalAvailability firewallAvailability = rules is not null
                ? OperationalAvailability.Available
                : managementSucceeded && daemonSucceeded
                    ? OperationalAvailability.Unavailable
                    : OperationalAvailability.Unknown;

            Current = new OperationalStatusSnapshot
            {
                ManagementApi = managementAvailability,
                Daemon = daemonAvailability,
                FirewallSnapshot = firewallAvailability,
                FirewallActive = rules?.Active,
                RuleCount = rules?.Rules.Count,
                IntentProtocolVersion = intentContext?.ProtocolVersion,
                IntentProtocolCompatible = intentContext is null
                    ? null
                    : intentContext.ProtocolVersion == IntentProtocol.VERSION,
                DeploymentId = intentContext?.DeploymentId,
                CheckedAt = timeProvider.GetUtcNow(),
                RoundTrip = timeProvider.GetElapsedTime(startedAt),
                ManagementApiError = DescribeFailure(managementTask),
                DaemonError = DescribeFailure(daemonTask),
                IntentContextError = DescribeFailure(intentContextTask),
                FirewallError = DescribeFailure(rulesTask),
            };
        }
        finally
        {
            IsRefreshing = false;
            Changed?.Invoke();
            _refreshGate.Release();
        }
    }

    private ClientError? DescribeFailure(Task task)
    {
        if (task.Exception is null)
        {
            return null;
        }

        Exception exception = task.Exception.InnerExceptions.Count == 1
            ? task.Exception.InnerException!
            : task.Exception;
        return clientErrors.Describe(exception);
    }

    public void Dispose() => _refreshGate.Dispose();
}
