namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints.Analyzers;

internal sealed record EndpointAnalyzerResult(
    string GenericParams,
    string RequestParam,
    string MethodArgs);