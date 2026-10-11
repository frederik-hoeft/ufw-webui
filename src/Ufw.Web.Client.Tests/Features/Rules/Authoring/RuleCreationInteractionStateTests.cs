using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Features.Rules.Insertion;

namespace Ufw.Web.Client.Tests.Features.Rules.Authoring;

[TestClass]
public sealed class RuleCreationInteractionStateTests
{
    [TestMethod]
    public void Initialization_PreventsMutationUntilReady()
    {
        RuleCreationInteractionState initial = RuleCreationInteractionState.Initial;
        Assert.AreEqual(RuleCreationPhase.Initializing, initial.Phase);
        Assert.IsFalse(initial.CanEdit(insertionRequested: false));
        Assert.IsFalse(initial.CanRefresh);
        Assert.ThrowsExactly<InvalidOperationException>(initial.ValidationStarted);

        RuleCreationInteractionState ready = initial.InitializationCompleted();
        Assert.IsTrue(ready.CanEdit(insertionRequested: false));
        Assert.IsTrue(ready.CanRefresh);
        Assert.IsFalse(ready.CanEdit(insertionRequested: true));
        Assert.ThrowsExactly<InvalidOperationException>(ready.InitializationCompleted);
    }

    [TestMethod]
    public void Validation_ClaimsSubmissionBeforeAsyncWorkAndCanReturnToReady()
    {
        RuleCreationInteractionState ready = RuleCreationInteractionState.Initial.InitializationCompleted();
        RuleCreationInteractionState validating = ready.ValidationStarted();

        Assert.IsTrue(validating.IsSubmitting);
        Assert.IsFalse(validating.CanEdit(insertionRequested: false));
        Assert.IsFalse(validating.CanRefresh);
        Assert.ThrowsExactly<InvalidOperationException>(validating.ValidationStarted);
        Assert.ThrowsExactly<InvalidOperationException>(() => validating.AddAwaitingConfirmation("rule"));

        RuleCreationInteractionState rejected = validating.ValidationStopped();
        Assert.AreEqual(RuleCreationPhase.Ready, rejected.Phase);
        Assert.IsTrue(rejected.CanEdit(insertionRequested: false));
        Assert.ThrowsExactly<InvalidOperationException>(rejected.ValidationStopped);
    }

    [TestMethod]
    public void AddFailure_OnlyAnUncertainOutcomeMayEnterConfirmation()
    {
        RuleCreationInteractionState submitting = Submitting();
        Assert.IsTrue(submitting.IsSubmitting);
        Assert.IsFalse(submitting.CanEdit(insertionRequested: false));
        Assert.ThrowsExactly<ArgumentException>(() => submitting.AddAwaitingConfirmation(" "));

        RuleCreationInteractionState rejected = submitting.AddRejected();
        Assert.IsTrue(rejected.CanEdit(insertionRequested: false));
        Assert.IsNull(rejected.PendingRuleId);

        RuleCreationInteractionState uncertain = submitting.AddAwaitingConfirmation("requested-id");
        Assert.AreEqual(RuleCreationPhase.AwaitingConfirmation, uncertain.Phase);
        Assert.AreEqual("requested-id", uncertain.PendingRuleId);
        Assert.IsTrue(uncertain.IsAwaitingConfirmation);
        Assert.IsTrue(uncertain.CanRefresh);
        Assert.IsFalse(uncertain.CanEdit(insertionRequested: false));
        Assert.ThrowsExactly<InvalidOperationException>(uncertain.ValidationStarted);
        Assert.ThrowsExactly<InvalidOperationException>(uncertain.AddRejected);
    }

    [TestMethod]
    public void AddConfirmation_RemainsPendingAcrossRefreshFailureUntilAuthoritativePresenceCheck()
    {
        RuleCreationInteractionState pending = Submitting().AddAwaitingConfirmation("confirmed-id");

        // A failed refresh does not produce a transition: retain the identity until a successful read.
        Assert.AreEqual("confirmed-id", pending.PendingRuleId);
        RuleCreationInteractionState completed = pending.AddPresenceChecked(isPresent: true);
        Assert.AreEqual(RuleCreationPhase.Completed, completed.Phase);
        Assert.IsNull(completed.PendingRuleId);
        Assert.IsFalse(completed.CanEdit(insertionRequested: false));
        Assert.IsFalse(completed.CanRefresh);

        RuleCreationInteractionState missing = pending.AddPresenceChecked(isPresent: false);
        Assert.AreEqual(RuleCreationPhase.Ready, missing.Phase);
        Assert.IsNull(missing.PendingRuleId);
        Assert.IsTrue(missing.CanEdit(insertionRequested: false));
        Assert.ThrowsExactly<InvalidOperationException>(() => missing.AddPresenceChecked(isPresent: true));
    }

    [TestMethod]
    [DataRow(RuleInsertionOutcome.StaleBaseline)]
    [DataRow(RuleInsertionOutcome.PreconditionFailed)]
    [DataRow(RuleInsertionOutcome.StateUncertain)]
    public void InsertionNonCompleted_RecordsResultWithoutClaimingSuccess(RuleInsertionOutcome outcome)
    {
        RuleInsertionResponse response = new(outcome, FinalSnapshot: null, InsertedRule: null, Diagnostic: "not completed");
        RuleCreationInteractionState result = Submitting().InsertionCompleted(response);

        Assert.AreEqual(RuleCreationPhase.Ready, result.Phase);
        Assert.AreSame(response, result.InsertionResult);
        Assert.IsFalse(result.IsAwaitingConfirmation);
        Assert.IsNull(result.PendingRuleId);
    }

    [TestMethod]
    public void InsertionCompleted_ConsumesSubmissionAndCannotBeAppliedTwice()
    {
        RuleInsertionResponse response = new(RuleInsertionOutcome.Completed, FinalSnapshot: null, InsertedRule: null, Diagnostic: null);
        RuleCreationInteractionState result = Submitting().InsertionCompleted(response);

        Assert.AreEqual(RuleCreationPhase.Completed, result.Phase);
        Assert.IsFalse(result.CanEdit(insertionRequested: false));
        Assert.ThrowsExactly<InvalidOperationException>(() => result.InsertionCompleted(response));
        Assert.ThrowsExactly<InvalidOperationException>(result.ValidationStarted);
    }

    [TestMethod]
    public void InsertionResult_RemainsVisibleAfterValidationFailureAndClearsOnNewSubmission()
    {
        RuleInsertionResponse response = new(RuleInsertionOutcome.PreconditionFailed, FinalSnapshot: null, InsertedRule: null, Diagnostic: "rejected");
        RuleCreationInteractionState ready = Submitting().InsertionCompleted(response);

        RuleCreationInteractionState validating = ready.ValidationStarted();
        Assert.AreSame(response, validating.InsertionResult);
        Assert.AreSame(response, validating.ValidationStopped().InsertionResult);
        Assert.IsNull(validating.SubmissionStarted().InsertionResult);
    }

    [TestMethod]
    public void InsertionContext_AvailableContextCanBeRefreshedUntilInvalidated()
    {
        RuleCreationInteractionState state = RuleCreationInteractionState.Initial.InitializationCompleted();
        OrderedRuleInsertionNavigationContext first = Context("first");
        OrderedRuleInsertionNavigationContext second = Context("second");

        state = state.InsertionResolved(new RuleInsertionNavigationResolution(first, OrderedRuleInsertionContextError.None));
        Assert.IsTrue(state.CanEdit(insertionRequested: true));
        Assert.AreSame(first, state.InsertionContext);

        state = state.InsertionResolved(new RuleInsertionNavigationResolution(second, OrderedRuleInsertionContextError.None));
        Assert.AreSame(second, state.InsertionContext);

        state = state.InvalidateInsertion();
        Assert.IsTrue(state.InsertionInvalidated);
        Assert.IsNull(state.InsertionContext);
        Assert.IsFalse(state.CanEdit(insertionRequested: true));
        Assert.IsTrue(state.CanEdit(insertionRequested: false));
        Assert.AreSame(state, state.InsertionResolved(new RuleInsertionNavigationResolution(first, OrderedRuleInsertionContextError.None)));
    }

    [TestMethod]
    public void InsertionResolutionFailure_IsTerminalAndPreservesDiagnostic()
    {
        RuleCreationInteractionState state = RuleCreationInteractionState.Initial.InitializationCompleted();
        state = state.InsertionResolved(new RuleInsertionNavigationResolution(Context("baseline"), OrderedRuleInsertionContextError.None));
        state = state.InsertionResolved(new RuleInsertionNavigationResolution(null, OrderedRuleInsertionContextError.StaleBaseline));

        Assert.IsTrue(state.InsertionInvalidated);
        Assert.AreEqual(OrderedRuleInsertionContextError.StaleBaseline, state.InsertionError);
        Assert.IsNull(state.InsertionContext);
        Assert.AreSame(state, state.InsertionResolved(new RuleInsertionNavigationResolution(Context("new"), OrderedRuleInsertionContextError.None)));
        Assert.ThrowsExactly<ArgumentException>(() => RuleCreationInteractionState.Initial.InsertionResolved(
            new RuleInsertionNavigationResolution(Context("bad"), OrderedRuleInsertionContextError.InvalidPlacement)));
    }

    [TestMethod]
    public void InsertionFailure_AllowsRetryOnlyWhenContextRemainsValid()
    {
        RuleCreationInteractionState state = RuleCreationInteractionState.Initial.InitializationCompleted();
        state = state.InsertionResolved(new RuleInsertionNavigationResolution(Context("baseline"), OrderedRuleInsertionContextError.None));
        state = state.ValidationStarted().SubmissionStarted().InsertionFailed();

        Assert.IsTrue(state.CanEdit(insertionRequested: true));
        state = state.InvalidateInsertion();
        Assert.IsFalse(state.CanEdit(insertionRequested: true));
        Assert.ThrowsExactly<InvalidOperationException>(state.InsertionFailed);
    }

    private static RuleCreationInteractionState Submitting() => RuleCreationInteractionState.Initial.InitializationCompleted().ValidationStarted().SubmissionStarted();

    private static OrderedRuleInsertionNavigationContext Context(string fingerprint) => new(
        fingerprint,
        AnchorOccurrenceId: 0,
        AnchorFamilyPosition: 1,
        RuleInsertionPlacement.Before,
        FirewallAddressFamily.IPv4,
        new ListedFirewallRule { DisplayNumber = 1, Parsed = true, RawLine = "test" });
}
