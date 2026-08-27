using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Ufw.Roslyn.SourceGen.Controllers.Diagnostics;
using Ufw.Roslyn.SourceGen.Controllers.Processors.Controllers;
using Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints.Analyzers;
using Ufw.Roslyn.SourceGen.Controllers.Processors.EndpointVerbs;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints;

internal sealed class EndpointProcessor(SourceProductionContext context, ControllerProcessingContext controllerContext)
{
    private readonly ImmutableArray<IEndpointSignatureAnalyzer> _signatureAnalyzers =
    [
        new EndpointReturnTypeAnalyzer(context),
        new EndpointParameterAnalyzer(context)
    ];

    public EndpointProcessorResult? Process(IMethodSymbol method, EndpointVerbProcessorResult endpointVerb)
    {
        // Construct full route
        string fullRoute = CombineRoutes(controllerContext.Route, endpointVerb.Route);
        if (string.IsNullOrEmpty(fullRoute))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MissingRoute,
                method.Locations.FirstOrDefault(),
                method.Name,
                controllerContext.ControllerTypeFullName));
            return null;
        }

        // Calculate final priority (lowest wins)
        long finalPriority = Math.Min(controllerContext.Priority ?? long.MaxValue, endpointVerb.Priority ?? long.MaxValue);
        if (finalPriority == long.MaxValue)
        {
            finalPriority = 0;
        }

        // Validate method signature and determine mapping type
        EndpointSignatureAnalyzerContext analyzerContext = new();
        foreach (IEndpointSignatureAnalyzer analyzer in _signatureAnalyzers)
        {
            if (!analyzer.TryAnalyze(method, analyzerContext))
            {
                return null;
            }
        }
        EndpointAnalyzerResult mappingInfo = analyzerContext.BuildEndpointMappingInfo();

        return new EndpointProcessorResult(
            endpointVerb.Verb,
            fullRoute,
            (int)finalPriority,
            mappingInfo.GenericParams,
            mappingInfo.RequestParam,
            mappingInfo.MethodArgs,
            controllerContext.ControllerTypeFullName,
            method.Name);
    }

    private static string CombineRoutes(string? controllerRoute, string? methodRoute)
    {
        string controller = controllerRoute?.Trim('/') ?? "";
        string method = methodRoute?.Trim('/') ?? "";

        if (string.IsNullOrEmpty(controller) && string.IsNullOrEmpty(method))
        {
            return "";
        }

        if (string.IsNullOrEmpty(controller))
        {
            return "/" + method;
        }

        if (string.IsNullOrEmpty(method))
        {
            return "/" + controller;
        }

        return "/" + controller + "/" + method;
    }
}
