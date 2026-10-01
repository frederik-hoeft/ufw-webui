using Ufw.Shared.Web;

namespace Ufw.Web.Client.Globals;

internal static class UriHelpers
{
    public static SimpleUriBuilder UriOf(ReadOnlySpan<char> root, int capacity = 256) => SimpleUriBuilder.Create(root, capacity);
}
