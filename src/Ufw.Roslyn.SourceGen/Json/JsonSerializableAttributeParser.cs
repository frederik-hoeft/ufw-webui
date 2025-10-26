using Microsoft.CodeAnalysis;

namespace Ufw.Roslyn.SourceGen.Json;

internal class JsonSerializableAttributeParser(SourceProductionContext context)
{
    public INamedTypeSymbol? GetTargetType(AttributeData jsonSerializableAttribute)
    {
        if (jsonSerializableAttribute.ConstructorArguments.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id: "GENERIC_JSON_TYPE_INFO_001",
                    title: "Invalid JsonSerializableAttribute",
                    messageFormat: "JsonSerializableAttribute must have exactly one constructor argument",
                    category: "Usage",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                jsonSerializableAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
            return null;
        }
        object? rawType = jsonSerializableAttribute.ConstructorArguments[0].Value;
        if (rawType is not INamedTypeSymbol type)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id: "GENERIC_JSON_TYPE_INFO_002",
                    title: "Invalid JsonSerializableAttribute",
                    messageFormat: "JsonSerializableAttribute constructor argument must be a Type",
                    category: "Usage",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                jsonSerializableAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
            return null;
        }
        return type;
    }
}
