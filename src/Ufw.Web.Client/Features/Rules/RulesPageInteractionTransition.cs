namespace Ufw.Web.Client.Features.Rules;

internal abstract record RulesPageInteractionTransition
{
    private RulesPageInteractionTransition()
    {
    }

    public sealed record DeleteDialogOpened : RulesPageInteractionTransition;
    public sealed record DeleteDialogClosed : RulesPageInteractionTransition;
    public sealed record DeleteConfirmed : RulesPageInteractionTransition;
    public sealed record DeleteCompleted : RulesPageInteractionTransition;
    public sealed record DisableDialogOpened : RulesPageInteractionTransition;
    public sealed record DisableDialogClosed : RulesPageInteractionTransition;
    public sealed record DisableConfirmed : RulesPageInteractionTransition;
    public sealed record DisableCompleted : RulesPageInteractionTransition;
    public sealed record MetadataDialogOpened : RulesPageInteractionTransition;
    public sealed record MetadataSaveStarted : RulesPageInteractionTransition;
    public sealed record MetadataSaveCompleted : RulesPageInteractionTransition;
    public sealed record MetadataDialogClosed : RulesPageInteractionTransition;
    public sealed record TemplateDialogOpened : RulesPageInteractionTransition;
    public sealed record TemplateSaveStarted : RulesPageInteractionTransition;
    public sealed record TemplateSaveCompleted : RulesPageInteractionTransition;
    public sealed record TemplateDialogClosed : RulesPageInteractionTransition;
    public sealed record ReorderStarted : RulesPageInteractionTransition;
    public sealed record ReorderCompleted : RulesPageInteractionTransition;
}
