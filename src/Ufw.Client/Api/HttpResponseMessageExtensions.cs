using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ufw.Client.Serialization;

namespace Ufw.Client.Api;

internal static class HttpResponseMessageExtensions
{
    public static async Task<T> ReadRequiredAsync<T>(this HttpResponseMessage response, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
    {
        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateException(response, content);
        }

        return DeserializeRequired(content, jsonTypeInfo);
    }

    public static async Task<T> ReadTransactionResponseAsync<T>(
        this HttpResponseMessage response,
        JsonTypeInfo<T> jsonTypeInfo,
        Func<T, bool> isTransactionResponse,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(isTransactionResponse);
        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        try
        {
            T value = DeserializeRequired(content, jsonTypeInfo);
            if (isTransactionResponse(value))
            {
                return value;
            }

            if (response.IsSuccessStatusCode)
            {
                throw new ApiProtocolException("The management API returned an invalid transaction response.");
            }
        }
        catch (ApiProtocolException) when (!response.IsSuccessStatusCode)
        {
            // Non-success responses may use the normal ProblemDetails contract instead of a transaction report.
        }

        throw CreateException(response, content);
    }

    public static async Task<ApiRequestException> CreateExceptionAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return CreateException(response, content);
    }

    private static T DeserializeRequired<T>(ReadOnlySpan<byte> content, JsonTypeInfo<T> jsonTypeInfo)
    {
        try
        {
            T? value = JsonSerializer.Deserialize(content, jsonTypeInfo);
            return value ?? throw new ApiProtocolException("The management API returned an empty response.");
        }
        catch (Exception exception) when (exception is NotSupportedException or JsonException)
        {
            throw new ApiProtocolException("The management API returned an invalid JSON response.", exception);
        }
    }

    private static ApiRequestException CreateException(HttpResponseMessage response, ReadOnlySpan<byte> content)
    {
        string message = $"The API request failed with status {(int)response.StatusCode}.";
        try
        {
            ApiProblemDetails? problem = JsonSerializer.Deserialize(content, ClientJsonSerializerContext.Default.ApiProblemDetails);
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
        catch (Exception exception) when (exception is NotSupportedException or JsonException)
        {
            // Preserve the status-based fallback for non-problem responses.
        }

        return new ApiRequestException(response.StatusCode, message, response.RequestMessage?.Method, response.RequestMessage?.RequestUri);
    }
}
