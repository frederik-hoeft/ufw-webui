using Microsoft.Extensions.Configuration;

namespace Ufw.Client.Configuration;

internal static class ClientRuntimeConfiguration
{
    private const string API_BASE_URL_KEY = "ApiBaseUrl";

    public static Uri GetApiBaseAddress(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? configuredValue = configuration[API_BASE_URL_KEY];
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            throw new InvalidOperationException("API base URL is not configured.");
        }

        if (!Uri.TryCreate(configuredValue, UriKind.Absolute, out Uri? address))
        {
            throw new InvalidOperationException("API base URL must be an absolute URI.");
        }

        if (!string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("API base URL must use HTTPS because refresh-token cookies are Secure.");
        }

        if (!string.IsNullOrEmpty(address.UserInfo)
            || !string.IsNullOrEmpty(address.Query)
            || !string.IsNullOrEmpty(address.Fragment))
        {
            throw new InvalidOperationException("API base URL cannot contain user information, a query string, or a fragment.");
        }

        string absoluteUri = address.AbsoluteUri;
        return absoluteUri.EndsWith('/')
            ? address
            : new Uri($"{absoluteUri}/", UriKind.Absolute);
    }
}
