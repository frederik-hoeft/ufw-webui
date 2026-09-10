using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Globalization;
using Ufw.Client.Storage;

namespace Ufw.Client.Localization;

internal sealed partial class BrowserClientCultureService(
    ClientLocalizationOptions options,
    ILocalStorage localStorage,
    NavigationManager navigation,
    ILogger<BrowserClientCultureService> logger) : IClientCultureService
{
    public CultureInfo CurrentCulture
    {
        get
        {
            CultureInfo current = CultureInfo.CurrentUICulture;
            return options.IsSupported(current.Name) ? current : options.DefaultCulture;
        }
    }

    public IReadOnlyList<ClientCultureOption> SupportedCultures => options.SupportedCultures;

    public async Task SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        if (!options.IsSupported(cultureName))
        {
            throw new ArgumentOutOfRangeException(nameof(cultureName), cultureName, "Unsupported client culture.");
        }

        if (string.Equals(CurrentCulture.Name, cultureName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await localStorage.SetItemAsync(options.StorageKey, cultureName, cancellationToken);
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
            LogCulturePreferenceWriteFailure(logger, exception);
            throw;
        }

        navigation.NavigateTo(navigation.Uri, forceLoad: true);
    }

    [LoggerMessage(LogLevel.Warning, "Could not persist the browser culture preference.")]
    private static partial void LogCulturePreferenceWriteFailure(ILogger logger, Exception exception);
}
