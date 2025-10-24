namespace Ufw.Roslyn.Controllers.Mapping.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ApiControllerMappingGeneratorAttribute<TEndpointMappingFactory, TRequestEnvelope, TResponseEnvelope> : Attribute 
    where TEndpointMappingFactory : IApiEndpointMappingFactory<TRequestEnvelope, TResponseEnvelope>;
