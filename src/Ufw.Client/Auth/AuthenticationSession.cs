using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Ufw.Client.Auth;

internal sealed class AuthenticationSession : AuthenticationStateProvider, IAuthenticationSession
{
    private static readonly AuthenticationState s_anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private AuthenticationState _state = s_anonymous;

    public string? AccessToken { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);

    public void SetToken(string accessToken, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        ClaimsPrincipal principal;
        try
        {
            principal = CreatePrincipal(accessToken);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new InvalidOperationException("The API returned an invalid access token.", exception);
        }

        AccessToken = accessToken;
        ExpiresAt = expiresAt;
        _state = new AuthenticationState(principal);
        NotifyAuthenticationStateChanged(Task.FromResult(_state));
    }

    public void Clear()
    {
        AccessToken = null;
        ExpiresAt = null;
        _state = s_anonymous;
        NotifyAuthenticationStateChanged(Task.FromResult(_state));
    }

    private static ClaimsPrincipal CreatePrincipal(string jwt)
    {
        string[] segments = jwt.Split('.');
        if (segments.Length != 3)
        {
            throw new InvalidOperationException("The API returned an invalid access token.");
        }

        byte[] payloadBytes = DecodeBase64Url(segments[1]);
        using JsonDocument document = JsonDocument.Parse(payloadBytes);
        List<Claim> claims = [];
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            AddClaims(claims, property.Name, property.Value);
        }

        string nameClaimType = claims.Any(static claim => claim.Type == "name") ? "name" : "email";
        string roleClaimType = claims.Any(static claim => claim.Type == "role") ? "role" : ClaimTypes.Role;
        ClaimsIdentity identity = new(claims, "Bearer", nameClaimType, roleClaimType);
        return new ClaimsPrincipal(identity);
    }

    private static void AddClaims(List<Claim> claims, string name, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement element in value.EnumerateArray())
            {
                AddClaim(claims, name, element);
            }
            return;
        }

        AddClaim(claims, name, value);
    }

    private static void AddClaim(List<Claim> claims, string name, JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            if (text is not null)
            {
                claims.Add(new Claim(name, text));
            }
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        string normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += (normalized.Length % 4) switch
        {
            0 => string.Empty,
            2 => "==",
            3 => "=",
            _ => throw new InvalidOperationException("The API returned an invalid access token."),
        };
        return Convert.FromBase64String(normalized);
    }
}
