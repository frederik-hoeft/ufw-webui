using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Insertion;

namespace Ufw.Web.Client.Features.Rules.Authoring;

/// <summary>
/// Tracks create-rule interaction legality separately from authoritative inventory freshness.
/// A pending add confirmation retains its semantic identity across failed inventory refreshes;
/// an invalidated ordered-insertion anchor can never be restored by another refresh.
/// </summary>
internal sealed record RuleCreationInteractionState
{
    private RuleCreationInteractionState()
    {
    }

    public static RuleCreationInteractionState Initial { get; } = new();

    public RuleCreationPhase Phase { get; private init; } = RuleCreationPhase.Initializing;
    public RuleCreationInsertionStatus InsertionStatus { get; private init; } = RuleCreationInsertionStatus.Unresolved;
    public OrderedRuleInsertionNavigationContext? InsertionContext { get; private init; }
    public OrderedRuleInsertionContextError InsertionError { get; private init; }
    public RuleInsertionResponse? InsertionResult { get; private init; }
    public string? PendingRuleId { get; private init; }

    public bool IsSubmitting => Phase is RuleCreationPhase.Validating or RuleCreationPhase.Submitting;
    public bool IsAwaitingConfirmation => Phase == RuleCreationPhase.AwaitingConfirmation;
    public bool InsertionInvalidated => InsertionStatus == RuleCreationInsertionStatus.Invalidated;
    public bool CanRefresh => Phase is RuleCreationPhase.Ready or RuleCreationPhase.AwaitingConfirmation;

    public bool CanEdit(bool insertionRequested) => Phase == RuleCreationPhase.Ready && (!insertionRequested || InsertionStatus == RuleCreationInsertionStatus.Available);

    public RuleCreationInteractionState InitializationCompleted()
    {
        EnsurePhase(RuleCreationPhase.Initializing);
        return this with { Phase = RuleCreationPhase.Ready };
    }

    public RuleCreationInteractionState ValidationStarted()
    {
        EnsurePhase(RuleCreationPhase.Ready);
        return this with { Phase = RuleCreationPhase.Validating };
    }

    public RuleCreationInteractionState ValidationStopped()
    {
        EnsurePhase(RuleCreationPhase.Validating);
        return this with { Phase = RuleCreationPhase.Ready };
    }

    public RuleCreationInteractionState SubmissionStarted()
    {
        EnsurePhase(RuleCreationPhase.Validating);
        return this with { Phase = RuleCreationPhase.Submitting, InsertionResult = null };
    }

    public RuleCreationInteractionState AddAwaitingConfirmation(string ruleId)
    {
        EnsurePhase(RuleCreationPhase.Submitting);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        return this with { Phase = RuleCreationPhase.AwaitingConfirmation, PendingRuleId = ruleId };
    }

    public RuleCreationInteractionState AddRejected()
    {
        EnsurePhase(RuleCreationPhase.Submitting);
        return this with { Phase = RuleCreationPhase.Ready };
    }

    public RuleCreationInteractionState AddPresenceChecked(bool isPresent)
    {
        EnsurePhase(RuleCreationPhase.AwaitingConfirmation);
        return this with { Phase = isPresent ? RuleCreationPhase.Completed : RuleCreationPhase.Ready, PendingRuleId = null };
    }

    public RuleCreationInteractionState InsertionCompleted(RuleInsertionResponse response)
    {
        EnsurePhase(RuleCreationPhase.Submitting);
        ArgumentNullException.ThrowIfNull(response);
        return this with
        {
            Phase = response.Outcome == RuleInsertionOutcome.Completed ? RuleCreationPhase.Completed : RuleCreationPhase.Ready,
            InsertionResult = response,
        };
    }

    public RuleCreationInteractionState InsertionFailed()
    {
        EnsurePhase(RuleCreationPhase.Submitting);
        return this with { Phase = RuleCreationPhase.Ready };
    }

    public RuleCreationInteractionState InsertionResolved(RuleInsertionNavigationResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (InsertionInvalidated)
        {
            return this;
        }
        if (resolution.Context is not { } context)
        {
            return InvalidateInsertion(resolution.Error);
        }
        if (resolution.Error != OrderedRuleInsertionContextError.None)
        {
            throw new ArgumentException("A resolved insertion context cannot have an error.", nameof(resolution));
        }

        return this with
        {
            InsertionStatus = RuleCreationInsertionStatus.Available,
            InsertionContext = context,
            InsertionError = OrderedRuleInsertionContextError.None,
        };
    }

    public RuleCreationInteractionState InvalidateInsertion(OrderedRuleInsertionContextError error = OrderedRuleInsertionContextError.None)
    {
        if (InsertionInvalidated)
        {
            return this;
        }

        return this with
        {
            InsertionStatus = RuleCreationInsertionStatus.Invalidated,
            InsertionContext = null,
            InsertionError = error,
        };
    }

    private void EnsurePhase(RuleCreationPhase expected)
    {
        if (Phase != expected)
        {
            throw new InvalidOperationException($"Create-rule transition requires '{expected}' but the current phase is '{Phase}'.");
        }
    }
}
