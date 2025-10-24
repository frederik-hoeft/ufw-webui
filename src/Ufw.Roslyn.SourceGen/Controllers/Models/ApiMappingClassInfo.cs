using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ufw.Roslyn.SourceGen.Controllers.Models;

internal sealed record ApiMappingClassInfo(
    INamedTypeSymbol ClassSymbol,
    INamedTypeSymbol FactoryType,
    INamedTypeSymbol RequestEnvelopeType,
    INamedTypeSymbol ResponseEnvelopeType,
    ImmutableArray<INamedTypeSymbol> ControllerRegistrations);