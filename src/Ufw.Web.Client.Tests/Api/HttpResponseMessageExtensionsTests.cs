using System.Net;
using System.Text;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.Auth;
using Ufw.Web.Model.V1.Auth;
using Ufw.Web.Model.V1.Errors;

namespace Ufw.Web.Client.Tests.Api;

[TestClass]
public sealed class HttpResponseMessageExtensionsTests
{
    [TestMethod]
    public async Task ReadRequiredAsync_SuccessReturnsTypedPayloadAsync()
    {
        using HttpResponseMessage response = JsonResponse(HttpStatusCode.OK, "{\"accessToken\":\"token\",\"expiresAt\":\"2026-09-11T18:00:00+00:00\"}");

        AuthTokenResponse result = await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.AuthTokenResponse, CancellationToken.None);

        Assert.AreEqual("token", result.AccessToken);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 11, 18, 0, 0, TimeSpan.Zero), result.ExpiresAt);
    }

    [TestMethod]
    public async Task ReadRequiredAsync_NullOrMalformedSuccessPayloadIsProtocolErrorAsync()
    {
        using HttpResponseMessage nullResponse = JsonResponse(HttpStatusCode.OK, "null");
        using HttpResponseMessage invalidResponse = JsonResponse(HttpStatusCode.OK, "not-json");

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() =>
            nullResponse.ReadRequiredAsync(ClientJsonSerializerContext.Default.AuthTokenResponse, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() =>
            invalidResponse.ReadRequiredAsync(ClientJsonSerializerContext.Default.AuthTokenResponse, CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateExceptionAsync_ProblemValidationErrorsTakePrecedenceAndPreserveRequestMetadataAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Put, "https://localhost/api/v1/resource");
        using HttpResponseMessage response = JsonResponse(
            HttpStatusCode.UnprocessableEntity,
            "{\"title\":\"title\",\"detail\":\"detail\",\"validationErrors\":[{\"propertyName\":\"field\",\"code\":\"first.code\",\"message\":\"first\"},{\"propertyName\":\"field\",\"code\":\"second.code\",\"message\":\"second\"}]}",
            request);

        ApiRequestException exception = await response.CreateExceptionAsync(CancellationToken.None);

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.AreEqual("first second", exception.Message);
        Assert.AreEqual(HttpMethod.Put, exception.Method);
        Assert.AreEqual(request.RequestUri, exception.RequestUri);
        CollectionAssert.AreEqual(
            new[]
            {
                new ApiValidationError("field", "first.code", "first"),
                new ApiValidationError("field", "second.code", "second"),
            },
            exception.ValidationErrors.ToArray());
    }

    [TestMethod]
    public async Task CreateExceptionAsync_LegacyMessageExtensionRemainsReadableDuringServerMigrationAsync()
    {
        using HttpResponseMessage response = JsonResponse(HttpStatusCode.BadRequest, "{\"message\":\"legacy detail\"}");

        ApiRequestException exception = await response.CreateExceptionAsync(CancellationToken.None);

        Assert.AreEqual("legacy detail", exception.Message);
        Assert.IsEmpty(exception.ValidationErrors);
    }

    [TestMethod]
    public async Task CreateExceptionAsync_NonProblemBodyUsesStatusFallbackAsync()
    {
        using HttpResponseMessage response = new(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("gateway html", Encoding.UTF8, "text/html"),
        };

        ApiRequestException exception = await response.CreateExceptionAsync(CancellationToken.None);

        Assert.AreEqual("The API request failed with status 502.", exception.Message);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json, HttpRequestMessage? request = null) => new(status)
    {
        RequestMessage = request,
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}
