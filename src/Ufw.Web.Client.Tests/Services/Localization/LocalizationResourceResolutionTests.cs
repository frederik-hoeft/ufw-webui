using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using Ufw.Web.Client.Services.Localization;

namespace Ufw.Web.Client.Tests.Services.Localization;

[TestClass]
[DoNotParallelize]
public sealed class LocalizationResourceResolutionTests
{
    [TestMethod]
    public void CommonStrings_GermanResourceResolvesFromServicesNamespace()
    {
        CultureInfo previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            ServiceCollection services = new();
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            using ServiceProvider provider = services.BuildServiceProvider();
            IStringLocalizer<CommonStrings> localizer = provider.GetRequiredService<IStringLocalizer<CommonStrings>>();

            Assert.AreEqual("Design", localizer["Theme"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [TestMethod]
    public void RulesStrings_GermanRuleEditResourceResolvesFromServicesNamespace()
    {
        CultureInfo previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            ServiceCollection services = new();
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            using ServiceProvider provider = services.BuildServiceProvider();
            IStringLocalizer<RulesStrings> localizer = provider.GetRequiredService<IStringLocalizer<RulesStrings>>();

            Assert.AreEqual("Firewall-Regel bearbeiten", localizer["EditRuleTitle"].Value);
            Assert.AreEqual("Firewall aktualisiert, aber Metadatenabgleich fehlgeschlagen", localizer["ReplacementMetadataReconciliationFailed"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [TestMethod]
    public void TemplatesStrings_GermanResourceResolvesFromServicesNamespace()
    {
        CultureInfo previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            ServiceCollection services = new();
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            using ServiceProvider provider = services.BuildServiceProvider();
            IStringLocalizer<TemplatesStrings> localizer = provider.GetRequiredService<IStringLocalizer<TemplatesStrings>>();

            Assert.AreEqual("Regelvorlagen", localizer["Title"].Value);
            Assert.AreEqual("Vorlage speichern", localizer["SaveTemplate"].Value);
            Assert.AreEqual("Vorlage wird hinzugefügt...", localizer["AddingTemplate"].Value);
            Assert.AreEqual("Vorlage verwenden", localizer["UseTemplate"].Value);
            Assert.AreEqual("Als Vorlage speichern", localizer["SaveAsTemplate"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
