using Microsoft.CodeAnalysis;

namespace Ufw.Roslyn.SourceGen.Controllers.Diagnostics;

internal static class DiagnosticDescriptors
{
    public static DiagnosticDescriptor InvalidMethodSignature { get; } = new(
        "UFWAPI001",
        "Invalid endpoint method signature",
        "Method '{0}' in controller '{1}' has an invalid signature. Expected: ValueTask<TResponse> MethodName([TRequest request, ]CancellationToken cancellationToken).",
        "UfwApiGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MissingRoute { get; } = new(
        "UFWAPI002",
        "Missing route information",
        "Method '{0}' in controller '{1}' must have a route specified either on the controller class or the method attribute",
        "UfwApiGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DuplicateEndpoints { get; } = new(
        "UFWAPI003",
        "Duplicate route mapping",
        "Route '{0}' with method '{1}' is defined multiple times. Each route + method combination must be unique.",
        "UfwApiGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MissingControllerRegistrations { get; } = new(
        "UFWAPI004",
        "No controller registrations found",
        "Class '{0}' is decorated with ApiControllerMappingGenerator but has no ApiControllerRegistration attributes",
        "UfwApiGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MultipleHttpVerbAttributes { get; } = new(
        "UFWAPI005",
        "Multiple HTTP verb attributes",
        "Method '{0}' has multiple HTTP verb attributes. Only one is allowed per method.",
        "UfwApiGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidMethodVisibility { get; } = new(
        "UFWAPI006",
        "Invalid method visibility",
        "Method '{0}' must be public and non-static to be used as an endpoint.",
        "UfwApiGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidReturnType { get; } = new(
        "UFWAPI07",
        "Invalid return type",
        "Method '{0}' has an invalid return type. Expected: ValueTask<TResponse>, where TResponse is the response envelope type.",
        "UfwApiGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ReturnTypeMustImplementIdentifiable { get; } = new(
        "UFWAPI08",
        "Return type must implement IIdentifiable",
        "The response type '{0}' returned by method '{1}' must implement the IIdentifiable interface.",
        "UfwApiGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
