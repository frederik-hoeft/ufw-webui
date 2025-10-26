namespace Ufw.Web.Pipeline;

internal interface IPipelineHandler
{
    int Priority { get; }
}
