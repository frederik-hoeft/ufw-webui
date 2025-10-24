using Microsoft.CodeAnalysis;

namespace Ufw.Roslyn.SourceGen.Controllers;

[Generator(LanguageNames.CSharp)]
public sealed class ApiEndpointBindingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) => throw new NotImplementedException();
}
