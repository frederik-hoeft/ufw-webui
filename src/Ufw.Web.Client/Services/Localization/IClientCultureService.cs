using System.Globalization;

namespace Ufw.Web.Client.Services.Localization;

internal interface IClientCultureService
{
    CultureInfo CurrentCulture { get; }

    IReadOnlyList<ClientCultureOption> SupportedCultures { get; }

    Task SetCultureAsync(string cultureName, CancellationToken cancellationToken = default);
}
