using MudBlazor;

namespace Ufw.Web.Client.UI.Components;

/// <summary>Consistent modal behavior and sizing for client dialogs.</summary>
public static class ClientDialogOptions
{
    public static DialogOptions Compact => Create(MaxWidth.ExtraSmall);

    public static DialogOptions Standard => Create(MaxWidth.Small);

    public static DialogOptions Wide => Create(MaxWidth.Medium);

    public static DialogOptions FocusedCompact => Create(MaxWidth.ExtraSmall, DefaultFocus.FirstChild);

    public static DialogOptions FocusedStandard => Create(MaxWidth.Small, DefaultFocus.FirstChild);

    // The rule-filter editor deliberately retains MudBlazor's default backdrop and Escape behavior.
    public static DialogOptions FilterEditor => new()
    {
        CloseButton = true,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
    };

    private static DialogOptions Create(MaxWidth width, DefaultFocus? focus = null) => new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        DefaultFocus = focus,
        FullWidth = true,
        MaxWidth = width,
    };
}
