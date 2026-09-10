using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ufw.Client.Serialization;

namespace Ufw.Client.Api;

internal static class HttpResponseMessageExtensions
{
    extension(HttpResponseMessage self)
    {
        public async Task<T> ReadRequiredAsync<T>(JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
        {
            if (!self.IsSuccessStatusCode)
            {
                throw await self.CreateExceptionAsync(cancellationToken);
            }

            try
            {
                T? value = await self.Content.ReadFromJsonAsync(jsonTypeInfo, cancellationToken);
                return value ?? throw new ApiProtocolException("The management API returned an empty response.");
            }
            catch (Exception exception) when (exception is NotSupportedException or System.Text.Json.JsonException)
            {
                throw new ApiProtocolException("The management API returned an invalid JSON response.", exception);
            }
        }

        public async Task<ApiRequestException> CreateExceptionAsync(CancellationToken cancellationToken)
        {
            string message = $"The API request failed with status {(int)self.StatusCode}.";
            try
            {
                ApiProblemDetails? problem = await self.Content.ReadFromJsonAsync(ClientJsonSerializerContext.Default.ApiProblemDetails, cancellationToken);
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
            catch (Exception exception) when (exception is HttpRequestException or NotSupportedException or JsonException)
            {
                // Preserve the status-based fallback for non-problem responses.
            }

            return new ApiRequestException(self.StatusCode, message, self.RequestMessage?.Method, self.RequestMessage?.RequestUri);
        }
    }
}
