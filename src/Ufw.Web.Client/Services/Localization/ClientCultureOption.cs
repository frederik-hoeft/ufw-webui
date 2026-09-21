using System.Globalization;

namespace Ufw.Web.Client.Services.Localization;

internal sealed record ClientCultureOption(CultureInfo Culture, string NativeDisplayName)
{
    public string Name => Culture.Name;
}
