using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Configuration.Swagger;

namespace Ufw.Web.Tests.Configuration.Swagger;

[TestClass]
public sealed class ResponseContractOpenApiTests
{
    private const string JSON_MEDIA_TYPE = "application/json";
    private const string PROBLEM_DETAILS_MEDIA_TYPE = "application/problem+json";

    [TestMethod]
    public async Task Generate_BodylessAuthenticationResponse_DoesNotAdvertiseProblemDetailsAsync()
    {
        await using WebApplication app = CreateApplication();
        OpenApiDocument document = GetDocument(app);

        IOpenApiResponse response = GetResponse(document, "/api/v1/rules", HttpMethod.Get, StatusCodes.Status401Unauthorized);

        IDictionary<string, OpenApiMediaType> content = GetContent(response);
        Assert.IsEmpty(content);
    }

    [TestMethod]
    public async Task Generate_ApplicationError_UsesPublicProblemDetailsContractAsync()
    {
        await using WebApplication app = CreateApplication();
        OpenApiDocument document = GetDocument(app);

        IOpenApiResponse response = GetResponse(document, "/api/v1/known-hosts", HttpMethod.Post, StatusCodes.Status400BadRequest);

        IDictionary<string, OpenApiMediaType> content = GetContent(response);
        Assert.HasCount(1, content);
        AssertSchemaReference(content[PROBLEM_DETAILS_MEDIA_TYPE].Schema, "ApiProblemDetails");
    }

    [TestMethod]
    public async Task Generate_SignedMutationForbiddenResponse_DocumentsBodylessAuthorizationAlternativeAsync()
    {
        await using WebApplication app = CreateApplication();
        OpenApiDocument document = GetDocument(app);

        IOpenApiResponse response = GetResponse(document, "/api/v1/rules/insert", HttpMethod.Post, StatusCodes.Status403Forbidden);

        IDictionary<string, OpenApiMediaType> content = GetContent(response);
        Assert.HasCount(1, content);
        AssertSchemaReference(content[PROBLEM_DETAILS_MEDIA_TYPE].Schema, "ApiProblemDetails");
        StringAssert.Contains(response.Description, "Framework authorization failures may be bodyless");
    }

    [TestMethod]
    public async Task Generate_TransactionConflict_AdvertisesTypedAndProblemDetailsAlternativesAsync()
    {
        await using WebApplication app = CreateApplication();
        OpenApiDocument document = GetDocument(app);

        IOpenApiResponse response = GetResponse(document, "/api/v1/rules/insert", HttpMethod.Post, StatusCodes.Status409Conflict);

        IDictionary<string, OpenApiMediaType> content = GetContent(response);
        Assert.HasCount(2, content);
        AssertSchemaReference(content[JSON_MEDIA_TYPE].Schema, "RuleInsertionResponse");
        AssertSchemaReference(content[PROBLEM_DETAILS_MEDIA_TYPE].Schema, "ApiProblemDetails");
    }

    [TestMethod]
    public async Task Generate_ReplacementInternalFailure_AdvertisesTypedAndProblemDetailsAlternativesAsync()
    {
        await using WebApplication app = CreateApplication();
        OpenApiDocument document = GetDocument(app);

        IOpenApiResponse response = GetResponse(document, "/api/v1/rules/replace", HttpMethod.Put, StatusCodes.Status500InternalServerError);

        IDictionary<string, OpenApiMediaType> content = GetContent(response);
        Assert.HasCount(2, content);
        AssertSchemaReference(content[JSON_MEDIA_TYPE].Schema, "RuleReplacementMutationResponse");
        AssertSchemaReference(content[PROBLEM_DETAILS_MEDIA_TYPE].Schema, "ApiProblemDetails");
    }

    [TestMethod]
    public async Task Generate_SignedMutationEndpoints_DeclareDaemonServerAndGatewayFailuresAsync()
    {
        await using WebApplication app = CreateApplication();
        OpenApiDocument document = GetDocument(app);
        (string Path, HttpMethod Method)[] operations =
        [
            ("/api/v1/rules", HttpMethod.Post),
            ("/api/v1/rules/insert", HttpMethod.Post),
            ("/api/v1/rules/replace", HttpMethod.Put),
            ("/api/v1/rules/order", HttpMethod.Put),
            ("/api/v1/rules/batch", HttpMethod.Delete),
            ("/api/v1/rules", HttpMethod.Delete),
        ];

        foreach ((string path, HttpMethod method) in operations)
        {
            _ = GetResponse(document, path, method, StatusCodes.Status500InternalServerError);
            IOpenApiResponse gatewayFailure = GetResponse(document, path, method, StatusCodes.Status502BadGateway);
            IDictionary<string, OpenApiMediaType> content = GetContent(gatewayFailure);
            AssertSchemaReference(content[PROBLEM_DETAILS_MEDIA_TYPE].Schema, "ApiProblemDetails", $"Unexpected gateway-failure schema for {method} {path}.");
        }
    }

    private static WebApplication CreateApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(RulesController).Assembly);
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader())
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });
        builder.Services.AddSwaggerGen();
        builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();
        return builder.Build();
    }

    private static OpenApiDocument GetDocument(WebApplication app)
    {
        ISwaggerProvider swagger = app.Services.GetRequiredService<ISwaggerProvider>();
        return swagger.GetSwagger("v1");
    }

    private static IOpenApiResponse GetResponse(OpenApiDocument document, string path, HttpMethod method, int statusCode)
    {
        IOpenApiPathItem pathItem = document.Paths[path];
        Dictionary<HttpMethod, OpenApiOperation> operations = pathItem.Operations ?? throw new AssertFailedException($"OpenAPI path {path} has no operations.");
        OpenApiOperation operation = operations[method];
        OpenApiResponses responses = operation.Responses ?? throw new AssertFailedException($"OpenAPI operation {method} {path} has no responses.");
        return responses[statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture)];
    }

    private static IDictionary<string, OpenApiMediaType> GetContent(IOpenApiResponse response) =>
        response.Content ?? throw new AssertFailedException("OpenAPI response has no content collection.");

    private static void AssertSchemaReference(IOpenApiSchema? schema, string expectedId, string? message = null)
    {
        Assert.IsInstanceOfType<OpenApiSchemaReference>(schema, message);
        OpenApiSchemaReference reference = (OpenApiSchemaReference)schema;
        Assert.AreEqual(expectedId, reference.Reference.Id, message);
    }
}
