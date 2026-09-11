using System.Globalization;

namespace Ufw.Client.Localization;

internal sealed record ClientCultureOption(CultureInfo Culture, string NativeDisplayName)
{
    public string Name => Culture.Name;
}
