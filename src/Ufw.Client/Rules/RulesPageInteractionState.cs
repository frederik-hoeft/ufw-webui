namespace Ufw.Client.Rules;

internal sealed record RulesPageInteractionState
{
    private RulesPageInteractionState(RulesPageInteractionMode mode)
    {
        Mode = mode;
    }

    public RulesPageInteractionMode Mode { get; }
    public bool IsBusy => Mode != RulesPageInteractionMode.Idle;
    public bool IsDeleting => Mode == RulesPageInteractionMode.Deleting;
    public bool IsReordering => Mode == RulesPageInteractionMode.Reordering;
    public bool CanMutateFirewall => Mode == RulesPageInteractionMode.Idle;
    public bool CanEditMetadata => Mode == RulesPageInteractionMode.Idle;
    public bool CanPreviewOrdering => Mode == RulesPageInteractionMode.Idle;
    public static RulesPageInteractionState Initial { get; } = new(RulesPageInteractionMode.Idle);

    public RulesPageInteractionState MoveNext(RulesPageInteractionTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        RulesPageInteractionMode next = (Mode, transition) switch
        {
            (RulesPageInteractionMode.Idle, RulesPageInteractionTransition.DeleteDialogOpened) => RulesPageInteractionMode.DeleteDialog,
            (RulesPageInteractionMode.DeleteDialog, RulesPageInteractionTransition.DeleteDialogClosed) => RulesPageInteractionMode.Idle,
            (RulesPageInteractionMode.DeleteDialog, RulesPageInteractionTransition.DeleteConfirmed) => RulesPageInteractionMode.Deleting,
            (RulesPageInteractionMode.Deleting, RulesPageInteractionTransition.DeleteCompleted) => RulesPageInteractionMode.Idle,
            (RulesPageInteractionMode.Idle, RulesPageInteractionTransition.MetadataDialogOpened) => RulesPageInteractionMode.MetadataDialog,
            (RulesPageInteractionMode.MetadataDialog, RulesPageInteractionTransition.MetadataSaveStarted) => RulesPageInteractionMode.MetadataSaving,
            (RulesPageInteractionMode.MetadataSaving, RulesPageInteractionTransition.MetadataSaveCompleted) => RulesPageInteractionMode.MetadataDialog,
            (RulesPageInteractionMode.MetadataDialog, RulesPageInteractionTransition.MetadataDialogClosed) => RulesPageInteractionMode.Idle,
            (RulesPageInteractionMode.Idle, RulesPageInteractionTransition.ReorderStarted) => RulesPageInteractionMode.Reordering,
            (RulesPageInteractionMode.Reordering, RulesPageInteractionTransition.ReorderCompleted) => RulesPageInteractionMode.Idle,
            _ => throw new InvalidOperationException($"Transition '{transition.GetType().Name}' is invalid while the rules page is in '{Mode}' mode."),
        };
        return next == RulesPageInteractionMode.Idle ? Initial : new RulesPageInteractionState(next);
    }
}
