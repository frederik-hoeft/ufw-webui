using System.Globalization;
using Ufw.Web.Client.UI.Formatting;

namespace Ufw.Web.Client.Tests.UI.Formatting;

[TestClass]
[DoNotParallelize]
public sealed class LocalDateTimeTextTests
{
    [TestMethod]
    [DataRow("en-US")]
    [DataRow("de-DE")]
    public void Format_UsesLocalTimeAndCurrentCulture(string cultureName)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            DateTimeOffset instant = new(2026, 10, 10, 17, 24, 0, TimeSpan.FromHours(7));

            string actual = LocalDateTimeText.Format(instant);

            Assert.AreEqual(instant.ToLocalTime().ToString("g", CultureInfo.CurrentCulture), actual);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
