using Microsoft.CodeAnalysis;
using Ufw.Roslyn.SourceGen.Controllers.Contracts;
using Ufw.Roslyn.SourceGen.Json.Contracts;

namespace Ufw.Roslyn.SourceGen.Contracts;

[Generator(LanguageNames.CSharp)]
public sealed class GeneratorContractBootstrapper : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddSource("ControllerGeneratorContract.g.cs", EmbeddedSourceText.FromType<ControllerGeneratorContract>());
            postInitializationContext.AddSource("JsonGeneratorContract.g.cs", EmbeddedSourceText.FromType<JsonGeneratorContract>());
            postInitializationContext.AddSource(
                "GeneratorContractRegistrationAttribute.g.cs",
                EmbeddedSourceText.FromType(typeof(GeneratorContractRegistrationAttribute<>)));
        });
    }
}
