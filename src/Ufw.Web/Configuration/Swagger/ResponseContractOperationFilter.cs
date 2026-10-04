using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Ufw.Web.Model.V1.Errors;

namespace Ufw.Web.Configuration.Swagger;

/// <summary>
/// Normalizes declared non-success responses so OpenAPI reflects the actual response body and media type contract.
/// </summary>
internal sealed class ResponseContractOperationFilter : IOperationFilter
{
    private const string JSON_MEDIA_TYPE = "application/json";
    private const string PROBLEM_DETAILS_MEDIA_TYPE = "application/problem+json";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        OpenApiResponses? responses = operation.Responses;
        if (responses is null)
        {
            return;
        }

        IEnumerable<IGrouping<int, ProducesResponseTypeAttribute>> responseGroups = context.MethodInfo
            .GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
            .Where(static response => response.StatusCode >= StatusCodes.Status400BadRequest)
            .GroupBy(static response => response.StatusCode);

        foreach (IGrouping<int, ProducesResponseTypeAttribute> responseGroup in responseGroups)
        {
            string statusCode = responseGroup.Key.ToString(CultureInfo.InvariantCulture);
            if (!responses.TryGetValue(statusCode, out IOpenApiResponse? declaredResponse) || declaredResponse is not OpenApiResponse response)
            {
                continue;
            }

            response.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.OrdinalIgnoreCase);

            Type[] responseTypes = responseGroup
                .Select(static declaration => declaration.Type)
                .Distinct()
                .ToArray();
            bool declaresProblemDetails = responseTypes.Contains(typeof(ApiProblemDetails));
            bool declaresBodylessResponse = responseTypes.Contains(typeof(void));
            if (!declaresProblemDetails)
            {
                if (responseTypes.All(static responseType => responseType == typeof(void)))
                {
                    response.Content.Clear();
                }
                continue;
            }

            Type[] typedResponses = responseTypes
                .Where(static responseType => responseType != typeof(void) && responseType != typeof(ApiProblemDetails))
                .ToArray();

            response.Content.Clear();
            if (declaresBodylessResponse)
            {
                string description = string.IsNullOrWhiteSpace(response.Description) ? "This response" : response.Description.TrimEnd('.');
                response.Description = $"{description}. Framework authorization failures may be bodyless; application failures use {PROBLEM_DETAILS_MEDIA_TYPE}.";
            }

            if (typedResponses.Length > 0)
            {
                response.Content[JSON_MEDIA_TYPE] = new OpenApiMediaType
                {
                    Schema = GenerateSchema(typedResponses, context),
                };
            }

            response.Content[PROBLEM_DETAILS_MEDIA_TYPE] = new OpenApiMediaType
            {
                Schema = context.SchemaGenerator.GenerateSchema(typeof(ApiProblemDetails), context.SchemaRepository),
            };
        }
    }

    private static IOpenApiSchema GenerateSchema(IReadOnlyList<Type> responseTypes, OperationFilterContext context)
    {
        if (responseTypes.Count == 1)
        {
            return context.SchemaGenerator.GenerateSchema(responseTypes[0], context.SchemaRepository);
        }

        IOpenApiSchema[] schemas = responseTypes
            .Select(responseType => context.SchemaGenerator.GenerateSchema(responseType, context.SchemaRepository))
            .ToArray();
        return new OpenApiSchema { OneOf = schemas };
    }
}
