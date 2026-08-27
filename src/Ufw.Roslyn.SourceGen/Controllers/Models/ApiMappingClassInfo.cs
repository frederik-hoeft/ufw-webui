using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Ufw.Roslyn.SourceGen.Controllers.Models;

internal sealed record ApiMappingClassInfo(
    INamedTypeSymbol ClassSymbol,
    INamedTypeSymbol FactoryType,
    INamedTypeSymbol RequestEnvelopeType,
    INamedTypeSymbol ResponseEnvelopeType,
    ImmutableArray<INamedTypeSymbol> ControllerRegistrations);
