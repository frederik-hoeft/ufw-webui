using Ufw.Shared.Web;

namespace Ufw.Web.Client.Api;

/// <summary>
/// Builds collection and GUID-addressed management API routes from trusted path segments. Resource clients retain their own request and response contracts.
/// </summary>
internal static class ApiResourceUri
{
    public static Uri ForId(string collectionPath, Guid id, string parameterName, string entityName, params string[] segments)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException($"{entityName} ID must not be empty.", parameterName);
        }

        SimpleUriBuilder uri = SimpleUriBuilder.Create(collectionPath).AppendPath(id.ToString("D"));
        foreach (string segment in segments)
        {
            uri.AppendPath(segment);
        }

        return uri.BuildUri(UriKind.Relative);
    }
}
