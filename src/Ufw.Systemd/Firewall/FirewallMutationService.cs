using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Model.Responses.Domain;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall;

internal sealed class FirewallMutationService(
    IUfwRunner ufwRunner,
    IIntentVerifier intentVerifier,
    INonceStore nonceStore,
    IUfwExecutionGate executionGate,
    ILogger logger) : IFirewallMutationService
{
    private readonly ILogger<FirewallMutationService> _logger = logger.Scoped<FirewallMutationService>();

    public ValueTask<IResponsePayload> ListAsync(CancellationToken cancellationToken) =>
        new(executionGate.RunAsync(ListUnsynchronizedAsync, cancellationToken));

    public async ValueTask<IResponsePayload> AddAsync(AddRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntentVerificationResult verification = intentVerifier.VerifyAdd(request);
        if (verification is IntentVerificationResult.Rejected rejected)
        {
            return rejected.Response;
        }

        IntentVerificationResult.Accepted accepted = (IntentVerificationResult.Accepted)verification;
        return await executionGate.RunAsync(
            ct => MutateAddUnsynchronizedAsync(accepted, ct),
            cancellationToken);
    }

    public async ValueTask<IResponsePayload> DeleteAsync(DeleteRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntentVerificationResult verification = intentVerifier.VerifyDelete(request);
        if (verification is IntentVerificationResult.Rejected rejected)
        {
            return rejected.Response;
        }

        IntentVerificationResult.Accepted accepted = (IntentVerificationResult.Accepted)verification;
        return await executionGate.RunAsync(
            ct => MutateDeleteUnsynchronizedAsync(accepted, ct),
            cancellationToken);
    }

    private async Task<IResponsePayload> ListUnsynchronizedAsync(CancellationToken cancellationToken)
    {
        (IResponsePayload? error, UfwStatusSnapshot? snapshot) = await ReadStatusAsync(cancellationToken);
        if (error is not null)
        {
            return error;
        }

        return ToListResponse(snapshot!);
    }

    private async Task<IResponsePayload> MutateAddUnsynchronizedAsync(
        IntentVerificationResult.Accepted accepted,
        CancellationToken cancellationToken)
    {
        if (!await nonceStore.TryConsumeAsync(accepted.Nonce, accepted.ExpiresAtUnix, cancellationToken))
        {
            return new ConflictResponse("Intent nonce has already been used.");
        }

        (IResponsePayload? listError, UfwStatusSnapshot? snapshot) = await ReadStatusAsync(cancellationToken);
        if (listError is not null)
        {
            return listError;
        }

        IReadOnlyList<string> identities = GetObservableIdentities(accepted.Rule);
        if (FindMatches(snapshot!, identities).Count > 0)
        {
            return new ConflictResponse("A semantically identical rule already exists.");
        }

        (IResponsePayload? executionError, UfwProcessResult? addResult) = await ExecuteProcessAsync(
            new UfwAddRuleCommand(accepted.Rule),
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

        (IResponsePayload? confirmError, UfwStatusSnapshot? confirmed) = await ReadStatusAsync(CancellationToken.None);
        if (confirmError is not null)
        {
            ThrowIfCancellationRequested(cancellationToken);
            return new InternalServerErrorResponse("UFW reported a successful add, but the resulting firewall state could not be confirmed.");
        }

        List<ListedFirewallRule> created = FindMatches(confirmed!, identities);
        if (created.Count == 0 || HasDuplicateIdentity(created))
        {
            _logger.LogError("UFW reported a successful add, but the expected semantic rule could not be reconciled uniquely.");
            ThrowIfCancellationRequested(cancellationToken);
            return new InternalServerErrorResponse("UFW reported a successful add, but the resulting firewall state could not be reconciled safely.");
        }

        ThrowIfCancellationRequested(cancellationToken);
        ListedFirewallRule responseRule = created[0];
        string identity = RuleIdentity.Compute(accepted.Rule);
        _logger.LogInformation($"Added firewall rule '{identity}'.");
        return new RuleMutationResponse(IntentOperations.ADD_RULE, responseRule);
    }

    private async Task<IResponsePayload> MutateDeleteUnsynchronizedAsync(
        IntentVerificationResult.Accepted accepted,
        CancellationToken cancellationToken)
    {
        if (!await nonceStore.TryConsumeAsync(accepted.Nonce, accepted.ExpiresAtUnix, cancellationToken))
        {
            return new ConflictResponse("Intent nonce has already been used.");
        }

        (IResponsePayload? listError, UfwStatusSnapshot? snapshot) = await ReadStatusAsync(cancellationToken);
        if (listError is not null)
        {
            return listError;
        }

        string identity = accepted.RuleId ?? RuleIdentity.Compute(accepted.Rule);
        List<ListedFirewallRule> matches = FindMatches(snapshot!, identity);
        if (matches.Count == 0)
        {
            return new NotFoundResponse("No current UFW rule matches the signed delete specification.");
        }

        if (matches.Count > 1)
        {
            return new ConflictResponse("Multiple current UFW rules match the signed delete specification.");
        }

        ListedFirewallRule match = matches[0];
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

        (IResponsePayload? confirmError, UfwStatusSnapshot? confirmed) = await ReadStatusAsync(CancellationToken.None);
        if (confirmError is not null)
        {
            ThrowIfCancellationRequested(cancellationToken);
            return new InternalServerErrorResponse("UFW reported a successful delete, but the resulting firewall state could not be confirmed.");
        }

        if (FindMatches(confirmed!, identity).Count > 0)
        {
            _logger.LogError($"UFW reported a successful delete, but firewall rule '{identity}' is still present.");
            ThrowIfCancellationRequested(cancellationToken);
            return new InternalServerErrorResponse("UFW reported a successful delete, but the rule is still present in the authoritative firewall state.");
        }

        ThrowIfCancellationRequested(cancellationToken);
        _logger.LogInformation($"Deleted firewall rule '{identity}'.");
        return new RuleMutationResponse(IntentOperations.DELETE_RULE, match);
    }

    private async Task<(IResponsePayload? Error, UfwStatusSnapshot? Snapshot)> ReadStatusAsync(CancellationToken cancellationToken)
    {
        UfwListCommand command = new();
        (IResponsePayload? executionError, UfwProcessResult? result) = await ExecuteProcessAsync(
            command,
            "Failed to start UFW while reading the current rule set.",
            cancellationToken);
        if (executionError is not null)
        {
            return (executionError, null);
        }

        if (result!.CancellationRequested)
        {
            ThrowCanceled(cancellationToken);
        }

        if (!result.Succeeded)
        {
            LogProcessFailure("status", result);
            return (new InternalServerErrorResponse("Failed to read the current UFW rule set."), null);
        }

        UfwStatusSnapshot? snapshot = await command.GetResultAsync(cancellationToken);
        if (snapshot is null)
        {
            _logger.LogError("UFW status returned successful process output that could not be parsed as a status response.");
            return (new InternalServerErrorResponse("Failed to parse the current UFW rule set."), null);
        }

        return (null, snapshot);
    }

    private async Task<(IResponsePayload? Error, UfwProcessResult? Result)> ExecuteProcessAsync(
        IUfwCommand command,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            UfwProcessResult result = await ufwRunner.ExecuteAsync(command, cancellationToken);
            return (null, result);
        }
        catch (ChildProcessException ex)
        {
            _logger.LogError(ex, failureMessage);
            return (new InternalServerErrorResponse(failureMessage), null);
        }
    }

    private async Task ReconcileInterruptedMutationAsync(
        string operation,
        IReadOnlyList<string> identities)
    {
        (IResponsePayload? error, UfwStatusSnapshot? snapshot) = await ReadStatusAsync(CancellationToken.None);
        if (error is not null)
        {
            _logger.LogWarning($"The canceled '{operation}' UFW process was reaped, but authoritative state could not be reconciled before releasing the execution gate.");
            return;
        }

        int observedMatches = FindMatches(snapshot!, identities).Count;
        _logger.LogWarning($"The '{operation}' request was canceled after UFW started. The child process was reaped and reconciliation observed {observedMatches} matching rule(s).");
    }

    private void LogProcessFailure(string operation, UfwProcessResult result)
    {
        string diagnostics = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        _logger.LogError($"ufw {operation} failed with exit code {result.ExitCode}: {diagnostics}");
    }

    private static void ThrowCanceled(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new OperationCanceledException("The UFW subprocess was canceled after it started.", cancellationToken);
    }

    private static void ThrowIfCancellationRequested(CancellationToken cancellationToken) =>
        cancellationToken.ThrowIfCancellationRequested();

    private static RuleListResponse ToListResponse(UfwStatusSnapshot snapshot)
    {
        List<ListedFirewallRule> rules = new(snapshot.Rules.Count);
        foreach (ObservedUfwRule observed in snapshot.Rules)
        {
            rules.Add(UfwRuleMapper.ToListedRule(observed));
        }

        return new RuleListResponse(snapshot.Active, rules);
    }

    private static List<ListedFirewallRule> FindMatches(UfwStatusSnapshot snapshot, string identity) =>
        FindMatches(snapshot, [identity]);

    private static List<ListedFirewallRule> FindMatches(UfwStatusSnapshot snapshot, IReadOnlyList<string> identities)
    {
        HashSet<string> identitySet = new(identities, StringComparer.Ordinal);
        List<ListedFirewallRule> matches = [];
        foreach (ObservedUfwRule observed in snapshot.Rules)
        {
            ListedFirewallRule listed = UfwRuleMapper.ToListedRule(observed);
            if (listed.Parsed && listed.RuleId is not null && identitySet.Contains(listed.RuleId))
            {
                matches.Add(listed);
            }
        }

        return matches;
    }

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

    private static FirewallRuleSpecification CloneWithAddressFamily(
        FirewallRuleSpecification source,
        FirewallAddressFamily addressFamily) => new()
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
