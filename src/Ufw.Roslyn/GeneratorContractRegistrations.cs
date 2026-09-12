using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Roslyn.Controllers.Mapping.Attributes;
using Ufw.Roslyn.Controllers.Routing;
using Ufw.Roslyn.Json;
using Ufw.Roslyn.SourceGen.Contracts;

[assembly: GeneratorContractRegistration(GeneratorContract.ApiControllerMappingTriggerAttribute, typeof(ApiControllerMappingGeneratorAttribute<,,>))]
[assembly: GeneratorContractRegistration(GeneratorContract.ApiControllerRegistrationAttribute, typeof(ApiControllerRegistrationAttribute<>))]
[assembly: GeneratorContractRegistration(GeneratorContract.ControllerRouteAttribute, typeof(RouteAttribute))]
[assembly: GeneratorContractRegistration(GeneratorContract.HttpGetRouteAttribute, typeof(GetAttribute))]
[assembly: GeneratorContractRegistration(GeneratorContract.HttpPostRouteAttribute, typeof(PostAttribute))]
[assembly: GeneratorContractRegistration(GeneratorContract.HttpPutRouteAttribute, typeof(PutAttribute))]
[assembly: GeneratorContractRegistration(GeneratorContract.HttpDeleteRouteAttribute, typeof(DeleteAttribute))]
[assembly: GeneratorContractRegistration(GeneratorContract.ApiEndpointMapping, typeof(ApiEndpointMapping<,>))]
[assembly: GeneratorContractRegistration(GeneratorContract.ControllerActivator, typeof(Ufw.Roslyn.Controllers.Internals.Activator))]
[assembly: GeneratorContractRegistration(GeneratorContract.IdentifiableResponse, typeof(IIdentifiable))]
[assembly: GeneratorContractRegistration(GeneratorContract.JsonTypeInfoBindingsTriggerAttribute, typeof(JsonTypeInfoBindingsGeneratorAttribute))]
[assembly: GeneratorContractRegistration(GeneratorContract.AotJsonSerializerContext, typeof(AotJsonSerializerContext))]
[assembly: GeneratorContractRegistration(GeneratorContract.JsonSerializableAttribute, typeof(JsonSerializableAttribute))]
[assembly: GeneratorContractRegistration(GeneratorContract.JsonTypeInfo, typeof(JsonTypeInfo<>))]
