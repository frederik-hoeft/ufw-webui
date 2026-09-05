using MudBlazor;

namespace Ufw.Client.Theming;

internal static class UfwMudTheme
{
    private static readonly string[] s_sansSerifFonts =
    [
        "IBM Plex Sans",
        "Segoe UI",
        "system-ui",
        "-apple-system",
        "BlinkMacSystemFont",
        "sans-serif",
    ];

    public static MudTheme Theme { get; } = CreateTheme();

    private static MudTheme CreateTheme()
    {
        Shadow shadows = new();
        shadows.Elevation[1] = "none";
        shadows.Elevation[4] = "none";

        return new MudTheme
        {
            PaletteLight = CreateLightPalette(),
            PaletteDark = CreateDarkPalette(),
            Typography = CreateTypography(),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "6px",
            },
            Shadows = shadows,
        };
    }

    private static PaletteLight CreateLightPalette() => new()
    {
        Black = "#0E141B",
        White = "#FFFFFF",
        Primary = "#2F6FDD",
        PrimaryContrastText = "#FFFFFF",
        Secondary = "#536071",
        SecondaryContrastText = "#FFFFFF",
        Tertiary = "#2459B6",
        TertiaryContrastText = "#FFFFFF",
        Info = "#2F6FDD",
        InfoContrastText = "#FFFFFF",
        Success = "#15803D",
        SuccessContrastText = "#FFFFFF",
        Warning = "#B45309",
        WarningContrastText = "#FFFFFF",
        Error = "#B83232",
        ErrorContrastText = "#FFFFFF",
        Dark = "#27313D",
        DarkContrastText = "#FFFFFF",
        TextPrimary = "#18202A",
        TextSecondary = "#536071",
        TextDisabled = "#8A96A6",
        ActionDefault = "#536071",
        ActionDisabled = "#8A96A6",
        ActionDisabledBackground = "#F1F4F7",
        Background = "#F7F9FB",
        BackgroundGray = "#F1F4F7",
        Surface = "#FFFFFF",
        DrawerBackground = "#0E141B",
        DrawerText = "#F1F4F7",
        DrawerIcon = "#8A96A6",
        AppbarBackground = "#FFFFFF",
        AppbarText = "#18202A",
        LinesDefault = "#E4E9EF",
        LinesInputs = "#AAB4C0",
        TableLines = "#E4E9EF",
        TableStriped = "#F7F9FB",
        TableHover = "#F1F4F7",
        Divider = "#E4E9EF",
        DividerLight = "#F1F4F7",
        Skeleton = "#E4E9EF",
        HoverOpacity = 0.08,
    };

    private static PaletteDark CreateDarkPalette() => new()
    {
        Black = "#0E141B",
        White = "#FFFFFF",
        Primary = "#6EA8FE",
        PrimaryContrastText = "#0E141B",
        Secondary = "#8A96A6",
        SecondaryContrastText = "#0E141B",
        Tertiary = "#6EA8FE",
        TertiaryContrastText = "#0E141B",
        Info = "#6EA8FE",
        InfoContrastText = "#0E141B",
        Success = "#22C55E",
        SuccessContrastText = "#0E141B",
        Warning = "#F59E0B",
        WarningContrastText = "#0E141B",
        Error = "#F87171",
        ErrorContrastText = "#0E141B",
        Dark = "#27313D",
        DarkContrastText = "#F1F4F7",
        TextPrimary = "#F1F4F7",
        TextSecondary = "#8A96A6",
        TextDisabled = "#687586",
        ActionDefault = "#8A96A6",
        ActionDisabled = "#536071",
        ActionDisabledBackground = "#27313D",
        Background = "#0E141B",
        BackgroundGray = "#27313D",
        Surface = "#18202A",
        DrawerBackground = "#0E141B",
        DrawerText = "#F1F4F7",
        DrawerIcon = "#8A96A6",
        AppbarBackground = "#18202A",
        AppbarText = "#F1F4F7",
        LinesDefault = "#3B4655",
        LinesInputs = "#536071",
        TableLines = "#3B4655",
        TableStriped = "#121921",
        TableHover = "#27313D",
        Divider = "#3B4655",
        DividerLight = "#27313D",
        Skeleton = "#27313D",
        HoverOpacity = 0.08,
    };

    private static Typography CreateTypography() => new()
    {
        Default = CreateTypographyPreset<DefaultTypography>("0.875rem", "400", "1.5"),
        H1 = CreateTypographyPreset<H1Typography>("2.25rem", "600", "1.2"),
        H2 = CreateTypographyPreset<H2Typography>("2rem", "600", "1.2"),
        H3 = CreateTypographyPreset<H3Typography>("1.875rem", "600", "1.2"),
        H4 = CreateTypographyPreset<H4Typography>("1.75rem", "600", "1.25"),
        H5 = CreateTypographyPreset<H5Typography>("1.25rem", "600", "1.3"),
        H6 = CreateTypographyPreset<H6Typography>("1.125rem", "600", "1.35"),
        Subtitle1 = CreateTypographyPreset<Subtitle1Typography>("1rem", "600", "1.4"),
        Subtitle2 = CreateTypographyPreset<Subtitle2Typography>("0.8125rem", "600", "1.4"),
        Body1 = CreateTypographyPreset<Body1Typography>("0.875rem", "400", "1.5"),
        Body2 = CreateTypographyPreset<Body2Typography>("0.875rem", "400", "1.5"),
        Button = CreateTypographyPreset<ButtonTypography>("0.8125rem", "600", "1.25"),
        Caption = CreateTypographyPreset<CaptionTypography>("0.75rem", "400", "1.4"),
        Overline = CreateTypographyPreset<OverlineTypography>("0.75rem", "600", "1.4"),
    };

    private static T CreateTypographyPreset<T>(string fontSize, string fontWeight, string lineHeight)
        where T : BaseTypography, new()
    {
        T typography = new()
        {
            FontFamily = s_sansSerifFonts,
            FontSize = fontSize,
            FontWeight = fontWeight,
            LineHeight = lineHeight,
            LetterSpacing = "0",
            TextTransform = "none",
        };
        return typography;
    }
}
