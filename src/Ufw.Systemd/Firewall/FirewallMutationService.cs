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

        UfwProcessResult addResult = await ufwRunner.ExecuteAsync(new UfwAddRuleCommand(accepted.Rule), cancellationToken);
        if (!addResult.Succeeded)
        {
            _logger.LogError($"ufw add failed with exit code {addResult.ExitCode}: {addResult.Output}");
            return new UnprocessableContentResponse("UFW rejected the add-rule request.");
        }

        (IResponsePayload? confirmError, UfwStatusSnapshot? confirmed) = await ReadStatusAsync(cancellationToken);
        if (confirmError is not null)
        {
            return confirmError;
        }

        ListedFirewallRule? created = FindMatches(confirmed!, identities).FirstOrDefault();
        string identity = RuleIdentity.Compute(accepted.Rule);
        _logger.LogInformation($"Added firewall rule '{identity}'.");
        return new RuleMutationResponse(IntentOperations.ADD_RULE, created);
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

        UfwProcessResult deleteResult = await ufwRunner.ExecuteAsync(new UfwDeleteRuleCommand(displayNumber), cancellationToken);
        if (!deleteResult.Succeeded)
        {
            _logger.LogError($"ufw delete failed with exit code {deleteResult.ExitCode}: {deleteResult.Output}");
            return new UnprocessableContentResponse("UFW rejected the delete-rule request.");
        }

        _logger.LogInformation($"Deleted firewall rule '{identity}'.");
        return new RuleMutationResponse(IntentOperations.DELETE_RULE, match);
    }

    private async Task<(IResponsePayload? Error, UfwStatusSnapshot? Snapshot)> ReadStatusAsync(CancellationToken cancellationToken)
    {
        UfwListCommand command = new();
        UfwProcessResult result = await ufwRunner.ExecuteAsync(command, cancellationToken);
        if (!result.Succeeded)
        {
            _logger.LogError($"ufw status failed with exit code {result.ExitCode}: {result.Output}");
            return (new InternalServerErrorResponse("Failed to read the current UFW rule set."), null);
        }

        UfwStatusSnapshot? snapshot = await command.GetResultAsync(cancellationToken);
        if (snapshot is null)
        {
            return (new InternalServerErrorResponse("Failed to parse the current UFW rule set."), null);
        }

        return (null, snapshot);
    }

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
