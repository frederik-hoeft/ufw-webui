using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall;

internal sealed class FirewallMutationExecutor(
    IFirewallRuleSnapshotReader snapshotReader,
    IFirewallRuleInterfaceValidator interfaceValidator,
    IUfwRunner ufwRunner,
    IUfwRuleCommandRenderer ufwRuleCommandRenderer,
    ILogger logger) : IFirewallMutationExecutor
{
    private readonly ILogger<FirewallMutationExecutor> _logger = logger.Scoped<FirewallMutationExecutor>();

    public async Task<IResponsePayload> AddAsync(IntentVerificationResult.Accepted intent, CancellationToken cancellationToken)
    {
        IResponsePayload? interfaceError = interfaceValidator.Validate(intent.Rule);
        if (interfaceError is not null)
        {
            return interfaceError;
        }

        IReadOnlyList<string> identities = GetObservableIdentities(intent.Rule);
        FirewallRuleSnapshotReadResult existingSnapshot = await snapshotReader.ReadAsync(cancellationToken);
        if (existingSnapshot.Error is not null)
        {
            return existingSnapshot.Error;
        }
        if (FirewallRuleSet.FindMatches(existingSnapshot.Snapshot!, identities).Count > 0)
        {
            return new ConflictResponse("A semantically identical rule already exists.");
        }

        (IResponsePayload? executionError, UfwProcessResult? addResult) = await ExecuteProcessAsync(
            new UfwAddRuleCommand(intent.Rule, ufwRuleCommandRenderer),
            "Failed to start the UFW add-rule operation.",
            cancellationToken);
        if (executionError is not null)
        {
            return executionError;
        }

        if (addResult!.CancellationRequested)
        {
            await ReconcileInterruptedMutationAsync(IntentOperations.ADD_RULE, identities);
            ThrowCanceled(cancellationToken);
        }

        if (!addResult.Succeeded)
        {
            LogProcessFailure("add", addResult);
            return new UnprocessableContentResponse("UFW rejected the add-rule request.");
        }

        FirewallRuleSnapshotReadResult confirmedSnapshot = await snapshotReader.ReadAsync(CancellationToken.None);
        if (confirmedSnapshot.Error is not null)
        {
            ThrowIfCancellationRequested(cancellationToken);
            return new InternalServerErrorResponse("UFW reported a successful add, but the resulting firewall state could not be confirmed.");
        }

        List<ListedFirewallRule> confirmedMatches = FirewallRuleSet.FindMatches(confirmedSnapshot.Snapshot!, identities);
        if (confirmedMatches.Count == 0 || HasDuplicateIdentity(confirmedMatches))
        {
            _logger.LogError("UFW reported a successful add, but the expected semantic rule could not be reconciled uniquely.");
            ThrowIfCancellationRequested(cancellationToken);
            return new InternalServerErrorResponse("UFW reported a successful add, but the resulting firewall state could not be reconciled safely.");
        }

        ThrowIfCancellationRequested(cancellationToken);
        ListedFirewallRule responseRule = confirmedMatches[0];
        string identity = RuleIdentity.Compute(intent.Rule);
        _logger.LogInformation($"Added firewall rule '{identity}'.");
        return new RuleMutationResponse(IntentOperations.ADD_RULE, responseRule);
    }

    public async Task<IResponsePayload> DeleteAsync(IntentVerificationResult.Accepted intent, CancellationToken cancellationToken)
    {
        string identity = intent.RuleId ?? RuleIdentity.Compute(intent.Rule);
        FirewallRuleSnapshotReadResult currentSnapshot = await snapshotReader.ReadAsync(cancellationToken);
        if (currentSnapshot.Error is not null)
        {
            return currentSnapshot.Error;
        }

        List<ListedFirewallRule> currentMatches = FirewallRuleSet.FindMatches(currentSnapshot.Snapshot!, identity);
        if (currentMatches.Count == 0)
        {
            return new NotFoundResponse("No current UFW rule matches the signed delete specification.");
        }
        if (currentMatches.Count > 1)
        {
            return new ConflictResponse("Multiple current UFW rules match the signed delete specification.");
        }

        ListedFirewallRule match = currentMatches[0];
        if (match.DisplayNumber is not int displayNumber)
        {
            return new UnprocessableContentResponse("Matched rule does not have a current UFW number.");
        }

        (IResponsePayload? executionError, UfwProcessResult? deleteResult) = await ExecuteProcessAsync(
            new UfwDeleteRuleCommand(displayNumber),
            "Failed to start the UFW delete-rule operation.",
            cancellationToken);
        if (executionError is not null)
        {
            return executionError;
        }

        if (deleteResult!.CancellationRequested)
        {
            await ReconcileInterruptedMutationAsync(IntentOperations.DELETE_RULE, [identity]);
            ThrowCanceled(cancellationToken);
        }

        if (!deleteResult.Succeeded)
        {
            LogProcessFailure("delete", deleteResult);
            return new UnprocessableContentResponse("UFW rejected the delete-rule request.");
        }

        FirewallRuleSnapshotReadResult confirmedSnapshot = await snapshotReader.ReadAsync(CancellationToken.None);
        if (confirmedSnapshot.Error is not null)
        {
            ThrowIfCancellationRequested(cancellationToken);
            return new InternalServerErrorResponse("UFW reported a successful delete, but the resulting firewall state could not be confirmed.");
        }
        if (FirewallRuleSet.FindMatches(confirmedSnapshot.Snapshot!, identity).Count > 0)
        {
            _logger.LogError($"UFW reported a successful delete, but firewall rule '{identity}' is still present.");
            ThrowIfCancellationRequested(cancellationToken);
            return new InternalServerErrorResponse("UFW reported a successful delete, but the rule is still present in the authoritative firewall state.");
        }

        ThrowIfCancellationRequested(cancellationToken);
        _logger.LogInformation($"Deleted firewall rule '{identity}'.");
        return new RuleMutationResponse(IntentOperations.DELETE_RULE, match);
    }

    private async Task<(IResponsePayload? Error, UfwProcessResult? Result)> ExecuteProcessAsync(IUfwCommand command, string failureMessage, CancellationToken cancellationToken)
    {
        try
        {
            UfwProcessResult result = await ufwRunner.ExecuteAsync(command, cancellationToken);
            return (null, result);
        }
        catch (ChildProcessException exception)
        {
            _logger.LogError(exception, failureMessage);
            return (new InternalServerErrorResponse(failureMessage), null);
        }
    }

    private async Task ReconcileInterruptedMutationAsync(string operation, IReadOnlyList<string> identities)
    {
        FirewallRuleSnapshotReadResult reconciliation = await snapshotReader.ReadAsync(CancellationToken.None);
        if (reconciliation.Error is not null)
        {
            _logger.LogWarning($"The canceled '{operation}' UFW process was reaped, but authoritative state could not be reconciled before releasing the execution gate.");
            return;
        }

        int observedMatches = FirewallRuleSet.FindMatches(reconciliation.Snapshot!, identities).Count;
        _logger.LogWarning($"The '{operation}' request was canceled after UFW started. The child process was reaped and reconciliation observed {observedMatches} matching rule(s).");
    }

    private void LogProcessFailure(string operation, UfwProcessResult result)
    {
        string diagnostics = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        _logger.LogError($"ufw {operation} failed with exit code {result.ExitCode}: {diagnostics}");
    }

    private static void ThrowCanceled(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new OperationCanceledException("The UFW subprocess was canceled after it started.", cancellationToken);
    }

    private static void ThrowIfCancellationRequested(CancellationToken cancellationToken) =>
        cancellationToken.ThrowIfCancellationRequested();

    private static bool HasDuplicateIdentity(IReadOnlyList<ListedFirewallRule> rules)
    {
        HashSet<string> identities = new(StringComparer.Ordinal);
        foreach (ListedFirewallRule rule in rules)
        {
            if (rule.RuleId is not null && !identities.Add(rule.RuleId))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> GetObservableIdentities(FirewallRuleSpecification specification)
    {
        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(specification);
        if (normalized.AddressFamily != FirewallAddressFamily.Any)
        {
            return [RuleIdentity.Compute(normalized)];
        }

        FirewallRuleSpecification ipv4 = CloneWithAddressFamily(normalized, FirewallAddressFamily.IPv4);
        FirewallRuleSpecification ipv6 = CloneWithAddressFamily(normalized, FirewallAddressFamily.IPv6);
        return [RuleIdentity.Compute(ipv4), RuleIdentity.Compute(ipv6)];
    }

    private static FirewallRuleSpecification CloneWithAddressFamily(FirewallRuleSpecification source, FirewallAddressFamily addressFamily) => new()
    {
        Action = source.Action,
        AddressFamily = addressFamily,
        Direction = source.Direction,
        Protocol = source.Protocol,
        Source = source.Source,
        SourcePorts = source.SourcePorts,
        SourceInterface = source.SourceInterface,
        Destination = source.Destination,
        DestinationPorts = source.DestinationPorts,
        DestinationInterface = source.DestinationInterface,
        Comment = source.Comment,
    };
}
