using System.Collections.Immutable;

namespace Ufw.Ipc.Shared.Pipelines;

public static class PipelineHandlerExtensions
{
    public static ImmutableArray<T> CreatePipeline<T>(this IEnumerable<T> enumerable) where T : class, IPipelineHandler =>
        [.. enumerable.InPipelineOrder()];

    public static IEnumerable<T> InPipelineOrder<T>(this IEnumerable<T> pipeline) where T : class, IPipelineHandler =>
        pipeline.OrderBy(handler => handler.Priority);
}
