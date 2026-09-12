using System;

namespace Ufw.Roslyn.SourceGen.Contracts;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal sealed class GeneratorContractRegistrationAttribute(GeneratorContract contract, Type type) : Attribute
{
    public GeneratorContract Contract { get; } = contract;

    public Type Type { get; } = type;
}
