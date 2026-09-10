using System.Globalization;

namespace Ufw.Client.Localization;

internal sealed class ClientLocalizationOptions
{
    private const string SECTION_NAME = "Localization";

    private ClientLocalizationOptions(CultureInfo defaultCulture, IReadOnlyList<ClientCultureOption> supportedCultures, string storageKey)
    {
        DefaultCulture = defaultCulture;
        SupportedCultures = supportedCultures;
        StorageKey = storageKey;
    }

    public CultureInfo DefaultCulture { get; }

    public IReadOnlyList<ClientCultureOption> SupportedCultures { get; }

    public string StorageKey { get; }

    public static ClientLocalizationOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(SECTION_NAME);
        string defaultCultureName = RequireValue(section["DefaultCulture"], "Localization:DefaultCulture");
        string storageKey = RequireValue(section["StorageKey"], "Localization:StorageKey");
        string[] supportedCultureNames =
        [
            .. section.GetSection("SupportedCultures")
            .GetChildren()
            .Select(static child => child.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
        ];

        if (supportedCultureNames.Length == 0)
        {
            throw new InvalidOperationException("Localization:SupportedCultures must contain at least one culture.");
        }

        List<ClientCultureOption> supportedCultures = new(supportedCultureNames.Length);
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (string cultureName in supportedCultureNames)
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
            if (!names.Add(culture.Name))
            {
                throw new InvalidOperationException($"Localization:SupportedCultures contains duplicate culture '{culture.Name}'.");
            }

            supportedCultures.Add(new ClientCultureOption(culture, culture.NativeName));
        }

        CultureInfo defaultCulture = CultureInfo.GetCultureInfo(defaultCultureName);
        if (!names.Contains(defaultCulture.Name))
        {
            throw new InvalidOperationException("Localization:DefaultCulture must be included in Localization:SupportedCultures.");
        }

        return new ClientLocalizationOptions(defaultCulture, supportedCultures, storageKey);
    }

    public bool IsSupported(string? cultureName) => cultureName is not null
        && SupportedCultures.Any(option => string.Equals(option.Name, cultureName, StringComparison.OrdinalIgnoreCase));

    private static string RequireValue(string? value, string key) => !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException($"Required client configuration '{key}' is missing.");
}
