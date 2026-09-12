using System;

namespace Ufw.Roslyn.SourceGen.Contracts;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal sealed class GeneratorContractRegistrationAttribute<TContract>(TContract contract, Type type) : Attribute where TContract : struct, Enum
{
    public TContract Contract { get; } = contract;

    public Type Type { get; } = type;
}
