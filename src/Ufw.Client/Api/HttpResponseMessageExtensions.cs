using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using Ufw.Client.Serialization;

namespace Ufw.Client.Api;

internal static class HttpResponseMessageExtensions
{
    public static async Task<T> ReadRequiredAsync<T>(
        this HttpResponseMessage response,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await response.CreateExceptionAsync(cancellationToken);
        }

        T? value = await response.Content.ReadFromJsonAsync(jsonTypeInfo, cancellationToken);
        return value ?? throw new ApiRequestException(response.StatusCode, "The API returned an empty response.");
    }

    public static async Task<ApiRequestException> CreateExceptionAsync(
        this HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string message = $"The API request failed with status {(int)response.StatusCode}.";
        try
        {
            ApiProblemDetails? problem = await response.Content.ReadFromJsonAsync(
                ClientJsonSerializerContext.Default.ApiProblemDetails,
                cancellationToken);
            if (problem is not null)
            {
                if (problem.Errors is { Count: > 0 })
                {
                    string errors = string.Join(" ", problem.Errors.Values.SelectMany(static values => values));
                    message = string.IsNullOrWhiteSpace(errors) ? message : errors;
                }
                else if (!string.IsNullOrWhiteSpace(problem.Detail))
                {
                    message = problem.Detail;
                }
                else if (!string.IsNullOrWhiteSpace(problem.Message))
                {
                    message = problem.Message;
                }
                else if (!string.IsNullOrWhiteSpace(problem.Title))
                {
                    message = problem.Title;
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or NotSupportedException or System.Text.Json.JsonException)
        {
            // Preserve the status-based fallback for non-problem responses.
        }

        return new ApiRequestException(response.StatusCode, message);
    }
}
