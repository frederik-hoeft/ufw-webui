using System.Text.Json.Serialization;
using Ufw.Client.Api;

namespace Ufw.Client.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(AuthTokenResponse))]
[JsonSerializable(typeof(ApiProblemDetails))]
internal sealed partial class ClientJsonSerializerContext : JsonSerializerContext;
