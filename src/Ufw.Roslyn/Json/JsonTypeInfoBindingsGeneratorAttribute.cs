namespace Ufw.Roslyn.Json;

[AttributeUsage(AttributeTargets.Class)]
public sealed class JsonTypeInfoBindingsGeneratorAttribute : Attribute
{
    public BindingsGenerationMode GenerationMode { get; set; }
}
