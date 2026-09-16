using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Models.Rules;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Services.Rules;

internal sealed partial class RuleMetadataService(
    IDaemonRuleSource daemonRules,
    IRuleMetadataRepository repository,
    ILogger<RuleMetadataService> logger) : IRuleMetadataService
{
    private const int MAX_TAG_COUNT = 32;

    public async Task<RuleMetadataUpdateResult> UpdateAsync(
        string ruleId,
        UpdateRuleMetadataRequest request,
        CancellationToken cancellationToken = default)
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

        RuleMetadataItem? metadata = await repository.SaveAsync(ruleId, values, cancellationToken);
        return new RuleMetadataUpdateResult(
            RuleMetadataUpdateOutcome.Success,
            new RuleMetadataMutationResponse(metadata));
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
        string? group = NormalizeOptional(request.Group);
        string? notes = NormalizeOptional(request.Notes);
        if (group?.Length > RuleMetadataEntry.MAX_GROUP_LENGTH
            || notes?.Length > RuleMetadataEntry.MAX_NOTES_LENGTH
            || request.Tags is null
            || request.Tags.Count > MAX_TAG_COUNT)
        {
            values = null;
            return false;
        }

        Dictionary<string, RuleMetadataTagValues> tags = new(StringComparer.Ordinal);
        foreach (string? candidate in request.Tags)
        {
            string? name = NormalizeOptional(candidate);
            if (name is null || name.Length > RuleMetadataTagEntry.MAX_NAME_LENGTH)
            {
                values = null;
                return false;
            }

            string normalizedName = name.ToUpperInvariant();
            if (normalizedName.Length > RuleMetadataTagEntry.MAX_NAME_LENGTH)
            {
                values = null;
                return false;
            }

            tags.TryAdd(normalizedName, new RuleMetadataTagValues(name, normalizedName));
        }

        RuleMetadataTagValues[] normalizedTags = [.. tags.Values.OrderBy(static tag => tag.NormalizedName, StringComparer.Ordinal)];
        values = new RuleMetadataValues(group, notes, normalizedTags);
        return true;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [LoggerMessage(1, LogLevel.Warning, "Rule metadata cleanup failed after deleting firewall rule {RuleId}.")]
    private static partial void LogCleanupFailure(ILogger logger, string ruleId, Exception exception);
}
