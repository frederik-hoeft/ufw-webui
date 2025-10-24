using Microsoft.CodeAnalysis;
using Ufw.Roslyn.SourceGen.Controllers.Diagnostics;
using Ufw.Roslyn.SourceGen.Controllers.Models;
using Ufw.Roslyn.SourceGen.Controllers.Processors.Controllers;
using Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.BindingClasses;

internal sealed class BindingClassProcessor(SourceProductionContext context, Compilation compilation, ApiMappingClassInfo mappingClass)
{
    public SourceProductionContext Context { get; } = context;

    public ApiMappingClassInfo MappingClass { get; } = mappingClass;

    public Compilation Compilation { get; } = compilation;

    public BindingClassProcessorResult Process()
    {
        List<EndpointProcessorResult> mappings = [];

        // Process each registered controller
        ControllerProcessor controllerProcessor = new(this);
        foreach (INamedTypeSymbol controllerType in MappingClass.ControllerRegistrations)
        {
            List<EndpointProcessorResult> controllerMappings = controllerProcessor.Process(controllerType);
            mappings.AddRange(controllerMappings);
        }

        // Sort mappings by priority (lowest first)
        mappings.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        // ensure uniqueness of mappings
        HashSet<(string HttpMethod, string Route)> seenMappings = [];
        foreach (EndpointProcessorResult mapping in mappings)
        {
            (string HttpMethod, string Route) key = (mapping.HttpMethod, mapping.Route);
            if (seenMappings.Contains(key))
            {
                Context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateEndpoints,
                    location: null,
                    mapping.HttpMethod,
                    mapping.Route));
            }
            else
            {
                seenMappings.Add(key);
            }
        }

        return new BindingClassProcessorResult(MappingClass, mappings);
    }
}