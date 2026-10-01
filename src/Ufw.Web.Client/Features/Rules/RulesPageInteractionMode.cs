namespace Ufw.Web.Client.Features.Rules;

internal enum RulesPageInteractionMode
{
    Idle,
    DeleteDialog,
    Deleting,
    DisableDialog,
    Disabling,
    MetadataDialog,
    MetadataSaving,
    TemplateDialog,
    TemplateSaving,
    Reordering,
}
