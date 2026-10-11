using System.Globalization;

namespace Ufw.Web.Client.UI.Formatting;

internal static class LocalDateTimeText
{
    public static string Format(DateTimeOffset value) => value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
