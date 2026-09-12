using Microsoft.CodeAnalysis;

namespace Ufw.Roslyn.SourceGen.Contracts;

[Generator(LanguageNames.CSharp)]
public sealed class GeneratorContractBootstrapper : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddSource("GeneratorContract.g.cs", EmbeddedSourceText.FromType<GeneratorContract>());
            postInitializationContext.AddSource("GeneratorContractRegistrationAttribute.g.cs", EmbeddedSourceText.FromType<GeneratorContractRegistrationAttribute>());
        });
    }
}
