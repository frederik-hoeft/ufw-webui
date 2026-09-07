using System.Globalization;

namespace Ufw.Client.Localization;

internal interface IClientCultureService
{
    CultureInfo CurrentCulture { get; }

    IReadOnlyList<ClientCultureOption> SupportedCultures { get; }

    Task SetCultureAsync(string cultureName, CancellationToken cancellationToken = default);
}
