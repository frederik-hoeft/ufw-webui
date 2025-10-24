using Microsoft.CodeAnalysis;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.EndpointVerbs;

internal sealed record EndpointVerbProcessorResult(AttributeData VerbAttribute, string Verb, string? Route, int? Priority);
