using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using Ufw.Web.Client.Services.Localization;

namespace Ufw.Web.Client.Tests.Localization;

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
}
