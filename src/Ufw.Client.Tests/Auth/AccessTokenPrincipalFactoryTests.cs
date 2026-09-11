using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Ufw.Client.Auth;

namespace Ufw.Client.Tests.Auth;

[TestClass]
public sealed class AccessTokenPrincipalFactoryTests
{
    private readonly AccessTokenPrincipalFactory _factory = new();

    [TestMethod]
    public void CreatePrincipal_ProjectsSupportedClaimsAndAuthenticationMetadata()
    {
        string token = CreateToken(new
        {
            email = "admin@example.invalid",
            name = "Administrator",
            role = new[] { "admin", "operator" },
            count = 7,
            enabled = true,
            ignored = new { nested = true },
            omitted = (string?)null,
        });

        ClaimsPrincipal principal = _factory.CreatePrincipal(token);
        ClaimsIdentity identity = (ClaimsIdentity)principal.Identity!;

        Assert.IsTrue(identity.IsAuthenticated);
        Assert.AreEqual("Administrator", identity.Name);
        Assert.IsTrue(principal.IsInRole("admin"));
        Assert.IsTrue(principal.IsInRole("operator"));
        Assert.AreEqual("7", principal.FindFirst("count")?.Value);
        Assert.AreEqual("true", principal.FindFirst("enabled")?.Value);
        Assert.IsNull(principal.FindFirst("ignored"));
        Assert.IsNull(principal.FindFirst("omitted"));
    }

    [TestMethod]
    public void CreatePrincipal_UsesEmailAndFrameworkRoleClaimAsFallbacks()
    {
        string token = CreateToken(new Dictionary<string, object?>
        {
            ["email"] = "admin@example.invalid",
            [ClaimTypes.Role] = "admin",
        });

        ClaimsPrincipal principal = _factory.CreatePrincipal(token);

        Assert.AreEqual("admin@example.invalid", principal.Identity?.Name);
        Assert.IsTrue(principal.IsInRole("admin"));
    }

    [TestMethod]
    public void CreatePrincipal_InvalidSegmentCountIsRejected() =>
        Assert.ThrowsExactly<InvalidOperationException>(() => _factory.CreatePrincipal("not-a-jwt"));

    [TestMethod]
    public void CreatePrincipal_InvalidBase64PayloadIsRejected() =>
        Assert.ThrowsExactly<FormatException>(() => _factory.CreatePrincipal("e30.***.signature"));

    [TestMethod]
    public void CreatePrincipal_InvalidJsonPayloadIsRejected()
    {
        string payload = Base64UrlEncode(Encoding.UTF8.GetBytes("not-json"));
        Assert.Throws<JsonException>(() => _factory.CreatePrincipal($"e30.{payload}.signature"));
    }

    private static string CreateToken<T>(T payload)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload);
        return $"e30.{Base64UrlEncode(json)}.signature";
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
