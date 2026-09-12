namespace Ufw.Roslyn.SourceGen.Contracts;

internal enum GeneratorContract
{
    ApiControllerMappingTriggerAttribute = 1,
    ApiControllerRegistrationAttribute = 2,
    ControllerRouteAttribute = 3,
    HttpGetRouteAttribute = 4,
    HttpPostRouteAttribute = 5,
    HttpPutRouteAttribute = 6,
    HttpDeleteRouteAttribute = 7,
    ApiEndpointMapping = 8,
    ControllerActivator = 9,
    IdentifiableResponse = 10,
    JsonTypeInfoBindingsTriggerAttribute = 11,
    AotJsonSerializerContext = 12,
    JsonSerializableAttribute = 13,
    JsonTypeInfo = 14,
}
