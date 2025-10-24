using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ufw.Roslyn.SourceGen.Controllers;

[Generator(LanguageNames.CSharp)]
public sealed class ApiEndpointBindingGenerator : IIncrementalGenerator
{
    private const string API_ENDPOINT_MAPPING_FULL_NAME = "global::Ufw.Roslyn.Controllers.Mapping.ApiEndpointMapping";
    private const string API_CONTROLLER_MAPPING_GENERATOR_ATTRIBUTE_NAME = "ApiControllerMappingGeneratorAttribute";
    private const string API_CONTROLLER_REGISTRATION_ATTRIBUTE_NAME = "ApiControllerRegistrationAttribute";
    private const string ROUTE_ATTRIBUTE_NAME = "RouteAttribute";
    private const string GET_ATTRIBUTE_NAME = "GetAttribute";
    private const string POST_ATTRIBUTE_NAME = "PostAttribute";
    private const string PUT_ATTRIBUTE_NAME = "PutAttribute";
    private const string DELETE_ATTRIBUTE_NAME = "DeleteAttribute";
    private const string ACTIVATOR_FULL_NAME = "global::Ufw.Roslyn.Controllers.Internals.Activator";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find classes decorated with ApiControllerMappingGenerator attribute
        IncrementalValuesProvider<ApiMappingClassInfo> apiMappingClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateClass(s),
                transform: static (ctx, _) => GetApiMappingClassInfo(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // Combine with compilation for symbol information
        IncrementalValueProvider<(Compilation, ImmutableArray<ApiMappingClassInfo>)> compilationAndClasses = 
            context.CompilationProvider.Combine(apiMappingClasses.Collect());

        context.RegisterSourceOutput(compilationAndClasses, (spc, source) => Execute(source.Item1, source.Item2, spc));
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 } classDecl &&
               classDecl.AttributeLists
                   .SelectMany(al => al.Attributes)
                   .Any(attr => attr.Name.ToString().Contains(API_CONTROLLER_MAPPING_GENERATOR_ATTRIBUTE_NAME.Replace("Attribute", "")));
    }

    private static ApiMappingClassInfo? GetApiMappingClassInfo(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDecl)
        {
            return null;
        }

        SemanticModel semanticModel = context.SemanticModel;
        INamedTypeSymbol? classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol is null)
        {
            return null;
        }

        // Find ApiControllerMappingGenerator attribute
        AttributeData? mappingGeneratorAttr = null;
        List<INamedTypeSymbol> controllerRegistrations = [];

        foreach (AttributeData attr in classSymbol.GetAttributes())
        {
            string? attrName = attr.AttributeClass?.Name;
            if (attrName?.StartsWith("ApiControllerMappingGenerator") == true)
            {
                mappingGeneratorAttr = attr;
            }
            else if (attrName?.StartsWith("ApiControllerRegistration") == true)
            {
                if (attr.AttributeClass?.TypeArguments.FirstOrDefault() is INamedTypeSymbol controllerType)
                {
                    controllerRegistrations.Add(controllerType);
                }
            }
        }

        if (mappingGeneratorAttr is null || controllerRegistrations.Count == 0)
        {
            return null;
        }

        // Extract factory type and envelope types from the generic attribute
        if (mappingGeneratorAttr.AttributeClass?.TypeArguments.ElementAtOrDefault(0) is not INamedTypeSymbol factoryType ||
            mappingGeneratorAttr.AttributeClass?.TypeArguments.ElementAtOrDefault(1) is not INamedTypeSymbol requestEnvelopeType ||
            mappingGeneratorAttr.AttributeClass?.TypeArguments.ElementAtOrDefault(2) is not INamedTypeSymbol responseEnvelopeType)
        {
            return null;
        }

        return new ApiMappingClassInfo(
            classSymbol,
            factoryType,
            requestEnvelopeType,
            responseEnvelopeType,
            [.. controllerRegistrations]);
    }

    private static void Execute(Compilation compilation, ImmutableArray<ApiMappingClassInfo> mappingClasses, SourceProductionContext context)
    {
        foreach (ApiMappingClassInfo mappingClass in mappingClasses)
        {
            try
            {
                string source = GeneratePartialClassSource(compilation, mappingClass);
                context.AddSource($"{mappingClass.ClassSymbol.Name}.g.cs", source);
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "UFWAPI001",
                        "Error generating API endpoint mappings",
                        "Error generating API endpoint mappings for {0}: {1}",
                        "UfwApiGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None,
                    mappingClass.ClassSymbol.Name,
                    ex.Message));
            }
        }
    }

    private static string GeneratePartialClassSource(Compilation compilation, ApiMappingClassInfo mappingClass)
    {
        StringBuilder sb = new();
        string? namespaceName = mappingClass.ClassSymbol.ContainingNamespace?.ToDisplayString();
        string className = mappingClass.ClassSymbol.Name;
        
        // Generate unique names to avoid conflicts
        string mappingsFieldName = SymbolNameGenerator.MakeUnique("s_mappings");
        string getMappingsMethodName = "GetMappings";

        // Extract fully qualified type names
        string factoryFullName = mappingClass.FactoryType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string requestEnvelopeFullName = mappingClass.RequestEnvelopeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string responseEnvelopeFullName = mappingClass.ResponseEnvelopeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string compilerGeneratedFullName = typeof(CompilerGeneratedAttribute).FullName!;

        List<EndpointMapping> mappings = [];

        // Process each registered controller
        foreach (INamedTypeSymbol controllerType in mappingClass.ControllerRegistrations)
        {
            List<EndpointMapping> controllerMappings = ProcessController(compilation, controllerType, mappingClass.FactoryType);
            mappings.AddRange(controllerMappings);
        }

        // Sort mappings by priority (lowest first)
        mappings.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        // Start generating the source
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine($"namespace {namespaceName};");
            sb.AppendLine();
        }

        // Generate the partial class with mappings array
        sb.AppendLine(
            $$"""
            partial class {{className}}
            {
                [global::{{compilerGeneratedFullName}}]
                private static readonly {{API_ENDPOINT_MAPPING_FULL_NAME}}<{{requestEnvelopeFullName}}, {{responseEnvelopeFullName}}>[] {{mappingsFieldName}} =
                [
            """);

        // Generate mapping entries
        for (int i = 0; i < mappings.Count; i++)
        {
            EndpointMapping mapping = mappings[i];

            sb.AppendLine(
                $$"""
                        {{factoryFullName}}.Map{{mapping.GenericParams}}("{{mapping.HttpMethod}}", "{{mapping.Route}}", priority: {{mapping.Priority}}, static async (serviceProvider, initializeAsync{{mapping.RequestParam}}, cancellationToken) =>
                        {
                            {{mapping.ControllerTypeFullName}} controller = await {{ACTIVATOR_FULL_NAME}}.CreateControllerAsync<{{mapping.ControllerTypeFullName}}>(serviceProvider, initializeAsync, cancellationToken);
                            return await controller.{{mapping.MethodName}}({{mapping.MethodArgs}}cancellationToken);
                        }),
                """);
        }

        sb.AppendLine("    ];");
        sb.AppendLine();

        // Generate the GetMappings override
        sb.AppendLine(
            $$"""
                [global::{{compilerGeneratedFullName}}]
                protected override {{API_ENDPOINT_MAPPING_FULL_NAME}}<{{requestEnvelopeFullName}}, {{responseEnvelopeFullName}}>[] {{getMappingsMethodName}}() => {{mappingsFieldName}};
            }
            """);

        return sb.ToString();
    }

    private static List<EndpointMapping> ProcessController(Compilation compilation, INamedTypeSymbol controllerType, INamedTypeSymbol factoryType)
    {
        List<EndpointMapping> mappings = [];

        // Get controller route information
        string? controllerRoute = GetControllerRoute(controllerType);
        int? controllerPriority = GetControllerPriority(controllerType);

        // Process public methods with HTTP verb attributes
        foreach (ISymbol member in controllerType.GetMembers())
        {
            if (member is not IMethodSymbol method || 
                method.DeclaredAccessibility != Accessibility.Public ||
                method.IsStatic)
            {
                continue;
            }

            AttributeData? httpAttr = GetHttpVerbAttribute(method);
            if (httpAttr is null)
            {
                continue;
            }

            string? methodRoute = GetMethodRoute(httpAttr);
            int? methodPriority = GetMethodPriority(httpAttr);

            // Construct full route
            string fullRoute = CombineRoutes(controllerRoute, methodRoute);
            if (string.IsNullOrEmpty(fullRoute))
            {
                continue; // At least one route component must be present
            }

            // Calculate final priority (lowest wins)
            int finalPriority = Math.Min(controllerPriority ?? int.MaxValue, methodPriority ?? int.MaxValue);
            if (finalPriority == int.MaxValue)
            {
                finalPriority = 0;
            }

            // Validate method signature and determine mapping type
            MethodMappingInfo? mappingInfo = AnalyzeMethodSignature(method);
            if (mappingInfo is null)
            {
                continue;
            }

            string httpMethod = GetHttpMethodFromAttribute(httpAttr);

            mappings.Add(new EndpointMapping(
                httpMethod,
                fullRoute,
                finalPriority,
                mappingInfo.GenericParams,
                mappingInfo.RequestParam,
                mappingInfo.MethodArgs,
                controllerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                method.Name));
        }

        return mappings;
    }

    private static string? GetControllerRoute(INamedTypeSymbol controllerType)
    {
        AttributeData? routeAttr = controllerType.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == ROUTE_ATTRIBUTE_NAME);

        return routeAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString();
    }

    private static int? GetControllerPriority(INamedTypeSymbol controllerType)
    {
        AttributeData? routeAttr = controllerType.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == ROUTE_ATTRIBUTE_NAME);

        if (routeAttr is null)
        {
            return null;
        }

        KeyValuePair<string, TypedConstant> priorityArg = routeAttr.NamedArguments
            .FirstOrDefault(arg => arg.Key == "Priority");

        return priorityArg.Value.Value as int?;
    }

    private static AttributeData? GetHttpVerbAttribute(IMethodSymbol method)
    {
        return method.GetAttributes()
            .FirstOrDefault(attr =>
            {
                string? name = attr.AttributeClass?.Name;
                return name 
                    is GET_ATTRIBUTE_NAME 
                    or POST_ATTRIBUTE_NAME 
                    or PUT_ATTRIBUTE_NAME
                    or DELETE_ATTRIBUTE_NAME;
            });
    }

    private static string? GetMethodRoute(AttributeData httpAttr)
    {
        return httpAttr.ConstructorArguments.FirstOrDefault().Value?.ToString();
    }

    private static int? GetMethodPriority(AttributeData httpAttr)
    {
        KeyValuePair<string, TypedConstant> priorityArg = httpAttr.NamedArguments
            .FirstOrDefault(arg => arg.Key == "Priority");

        return priorityArg.Value.Value as int?;
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

    private static string GetHttpMethodFromAttribute(AttributeData httpAttr)
    {
        return httpAttr.AttributeClass?.Name switch
        {
            GET_ATTRIBUTE_NAME => "GET",
            POST_ATTRIBUTE_NAME => "POST",
            PUT_ATTRIBUTE_NAME => "PUT",
            DELETE_ATTRIBUTE_NAME => "DELETE",
            _ => "GET"
        };
    }

    private static MethodMappingInfo? AnalyzeMethodSignature(IMethodSymbol method)
    {
        // Must return ValueTask<TResponse>
        if (method.ReturnType is not INamedTypeSymbol returnType ||
            !IsValueTask(returnType))
        {
            return null;
        }

        ITypeSymbol? responseType = returnType.TypeArguments.FirstOrDefault();
        if (responseType is null)
        {
            return null;
        }

        // Check parameters: optional TRequest, required CancellationToken
        ImmutableArray<IParameterSymbol> parameters = method.Parameters;
        bool hasRequest = false;
        string requestTypeFullName = "";

        if (parameters.Length == 1)
        {
            // Only CancellationToken
            if (!IsCancellationToken(parameters[0].Type))
            {
                return null;
            }
        }
        else if (parameters.Length == 2)
        {
            // TRequest and CancellationToken
            if (!IsCancellationToken(parameters[1].Type))
            {
                return null;
            }

            hasRequest = true;
            requestTypeFullName = parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        else
        {
            return null; // Invalid parameter count
        }

        string responseTypeFullName = responseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (hasRequest)
        {
            return new MethodMappingInfo(
                $"<{requestTypeFullName}, {responseTypeFullName}>",
                ", request",
                "request, ");
        }
        else
        {
            return new MethodMappingInfo(
                $"<{responseTypeFullName}>",
                "",
                "");
        }
    }

    private static bool IsValueTask(INamedTypeSymbol type)
    {
        return type.Name == "ValueTask" &&
               type.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks";
    }

    private static bool IsCancellationToken(ITypeSymbol type)
    {
        return type.Name == "CancellationToken" &&
               type.ContainingNamespace?.ToDisplayString() == "System.Threading";
    }

    private record ApiMappingClassInfo(
        INamedTypeSymbol ClassSymbol,
        INamedTypeSymbol FactoryType,
        INamedTypeSymbol RequestEnvelopeType,
        INamedTypeSymbol ResponseEnvelopeType,
        ImmutableArray<INamedTypeSymbol> ControllerRegistrations);

    private record EndpointMapping(
        string HttpMethod,
        string Route,
        int Priority,
        string GenericParams,
        string RequestParam,
        string MethodArgs,
        string ControllerTypeFullName,
        string MethodName);

    private record MethodMappingInfo(
        string GenericParams,
        string RequestParam,
        string MethodArgs);
}
