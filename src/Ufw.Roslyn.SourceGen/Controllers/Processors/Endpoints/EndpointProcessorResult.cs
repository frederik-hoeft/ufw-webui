namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints;

internal sealed record EndpointProcessorResult(
    string HttpMethod,
    string Route,
    int Priority,
    string GenericParams,
    string RequestParam,
    string MethodArgs,
    string ControllerTypeFullName,
    string MethodName);
