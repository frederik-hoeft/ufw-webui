using Microsoft.Extensions.Configuration;
using Ufw.Client.Localization;

namespace Ufw.Client.Tests.Localization;

[TestClass]
public sealed class ClientLocalizationOptionsTests
{
    [TestMethod]
    public void FromConfiguration_BuildsNormalizedSupportedCultureSet()
    {
        ClientLocalizationOptions options = CreateOptions("en-US", "en-US", "de-DE");

        Assert.AreEqual("en-US", options.DefaultCulture.Name);
        Assert.AreEqual("ufw.culture", options.StorageKey);
        CollectionAssert.AreEqual(new[] { "en-US", "de-DE" }, options.SupportedCultures.Select(static culture => culture.Name).ToArray());
        Assert.IsTrue(options.IsSupported("DE-de"));
        Assert.IsFalse(options.IsSupported("fr-FR"));
    }

    [TestMethod]
    public void FromConfiguration_DefaultMustBeSupported()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => CreateOptions("fr-FR", "en-US", "de-DE"));
    }

    [TestMethod]
    public void FromConfiguration_DuplicateCulturesAreRejectedCaseInsensitively()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => CreateOptions("en-US", "en-US", "EN-us"));
    }

    [TestMethod]
    public void FromConfiguration_RequiresStorageKeyAndAtLeastOneCulture()
    {
        IConfiguration missingStorage = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Localization:DefaultCulture"] = "en-US",
            ["Localization:SupportedCultures:0"] = "en-US",
        }).Build();
        IConfiguration missingCultures = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Localization:DefaultCulture"] = "en-US",
            ["Localization:StorageKey"] = "ufw.culture",
        }).Build();

        Assert.ThrowsExactly<InvalidOperationException>(() => ClientLocalizationOptions.FromConfiguration(missingStorage));
        Assert.ThrowsExactly<InvalidOperationException>(() => ClientLocalizationOptions.FromConfiguration(missingCultures));
    }

    internal static ClientLocalizationOptions CreateOptions(string defaultCulture, params string[] supportedCultures)
    {
        Dictionary<string, string?> values = new()
        {
            ["Localization:DefaultCulture"] = defaultCulture,
            ["Localization:StorageKey"] = "ufw.culture",
        };
        for (int index = 0; index < supportedCultures.Length; index++)
        {
            values[$"Localization:SupportedCultures:{index}"] = supportedCultures[index];
        }

        return ClientLocalizationOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }
}
