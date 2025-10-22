using System.Collections.Immutable;

namespace UfwWebUI.Pipeline;

internal static class PipelineHandlerExtensions
{
    public static ImmutableArray<T> CreatePipeline<T>(this IEnumerable<T> enumerable) where T : class, IPipelineHandler =>
        [.. enumerable.OrderBy(handler => handler.Priority)];
}
