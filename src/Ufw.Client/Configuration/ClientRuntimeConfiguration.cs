using Microsoft.Extensions.Configuration;

namespace Ufw.Client.Configuration;

internal static class ClientRuntimeConfiguration
{
    private const string API_BASE_URL_KEY = "ApiBaseUrl";

    public static Uri GetApiBaseAddress(IConfiguration configuration, Uri applicationBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(applicationBaseAddress);

        string? configuredValue = configuration[API_BASE_URL_KEY];
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            throw new InvalidOperationException("API base URL is not configured.");
        }

        Uri? address;
        if (Uri.IsWellFormedUriString(configuredValue, UriKind.Absolute) 
            && Uri.TryCreate(configuredValue, UriKind.Absolute, out Uri? absoluteAddress))
        {
            address = absoluteAddress;
        }
        else if (Uri.IsWellFormedUriString(configuredValue, UriKind.Relative)
            && !configuredValue.StartsWith("//", StringComparison.Ordinal)
            && Uri.TryCreate(applicationBaseAddress, configuredValue, out Uri? relativeAddress))
        {
            address = relativeAddress;
        }
        else
        {
            throw new InvalidOperationException("API base URL must be an absolute URI or a valid application-relative URI.");
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
