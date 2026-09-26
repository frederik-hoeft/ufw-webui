using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Services.Rules;

internal sealed partial class RuleMetadataService(IDaemonRuleSource daemonRules, IRuleMetadataRepository repository, ILogger<RuleMetadataService> logger) : IRuleMetadataService
{
    private const int MAX_TAG_COUNT = 32;

    public async Task<RuleMetadataUpdateResult> UpdateAsync(string ruleId, UpdateRuleMetadataRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(request);
        if (!TryNormalize(request, out RuleMetadataValues? values))
        {
            return new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.InvalidMetadata);
        }

        RuleListResponse snapshot = await daemonRules.GetAsync(cancellationToken);
        bool exists = snapshot.Rules.Any(rule => string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal));
        if (!exists)
        {
            return new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.RuleNotFound);
        }

        RuleMetadataSaveResult save = await repository.SaveAsync(ruleId, values, cancellationToken);
        return save.Outcome switch
        {
            RuleMetadataSaveOutcome.Success => new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.Success, new RuleMetadataMutationResponse { Metadata = save.Metadata }),
            RuleMetadataSaveOutcome.TagNotFound => new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.TagNotFound),
            RuleMetadataSaveOutcome.GroupNotFound => new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.GroupNotFound),
            _ => throw new InvalidOperationException($"Unknown metadata save outcome '{save.Outcome}'."),
        };
    }

    public async Task RemoveForDeletedRuleAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        try
        {
            _ = await repository.DeleteAsync(ruleId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogCleanupFailure(logger, ruleId, exception);
        }
    }

    private static bool TryNormalize(UpdateRuleMetadataRequest request, [NotNullWhen(true)] out RuleMetadataValues? values)
    {
        string? notes = NormalizeOptional(request.Notes);
        if (notes?.Length > RuleMetadataEntry.MAX_NOTES_LENGTH
            || request.TagIds is null
            || request.TagIds.Count > MAX_TAG_COUNT
            || request.TagIds.Any(static id => id == Guid.Empty)
            || request.GroupId == Guid.Empty)
        {
            values = null;
            return false;
        }

        Guid[] tagIds = [.. request.TagIds.Distinct().Order()];
        values = new RuleMetadataValues(notes, tagIds, request.GroupId);
        return true;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [LoggerMessage(1, LogLevel.Warning, "Rule metadata cleanup failed after deleting firewall rule {RuleId}.")]
    private static partial void LogCleanupFailure(ILogger logger, string ruleId, Exception exception);
}
