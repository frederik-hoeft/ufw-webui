using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Roslyn.Controllers.Mapping.Attributes;
using Ufw.Roslyn.Controllers.Routing;
using Ufw.Roslyn.SourceGen.Contracts;
using Ufw.Roslyn.SourceGen.Controllers.Contracts;

[assembly: GeneratorContractRegistration<ControllerGeneratorContract>(ControllerGeneratorContract.ApiControllerMappingTriggerAttribute, typeof(ApiControllerMappingGeneratorAttribute<,,>))]
[assembly: GeneratorContractRegistration<ControllerGeneratorContract>(ControllerGeneratorContract.ApiControllerRegistrationAttribute, typeof(ApiControllerRegistrationAttribute<>))]
[assembly: GeneratorContractRegistration<ControllerGeneratorContract>(ControllerGeneratorContract.ControllerRouteAttribute, typeof(RouteAttribute))]
[assembly: GeneratorContractRegistration<ControllerGeneratorContract>(ControllerGeneratorContract.HttpGetRouteAttribute, typeof(GetAttribute))]
[assembly: GeneratorContractRegistration<ControllerGeneratorContract>(ControllerGeneratorContract.HttpPostRouteAttribute, typeof(PostAttribute))]
[assembly: GeneratorContractRegistration<ControllerGeneratorContract>(ControllerGeneratorContract.HttpPutRouteAttribute, typeof(PutAttribute))]
[assembly: GeneratorContractRegistration<ControllerGeneratorContract>(ControllerGeneratorContract.HttpDeleteRouteAttribute, typeof(DeleteAttribute))]
[assembly: GeneratorContractRegistration<ControllerGeneratorContract>(ControllerGeneratorContract.ApiEndpointMapping, typeof(ApiEndpointMapping<,>))]
[assembly: GeneratorContractRegistration<ControllerGeneratorContract>(ControllerGeneratorContract.ControllerActivator, typeof(Ufw.Roslyn.Controllers.Internals.Activator))]
[assembly: GeneratorContractRegistration<ControllerGeneratorContract>(ControllerGeneratorContract.IdentifiableResponse, typeof(IIdentifiable))]
