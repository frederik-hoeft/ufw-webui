using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Roslyn.Controllers.Mapping.Attributes;
using Ufw.Roslyn.Controllers.Routing;
using Ufw.Roslyn.SourceGen.Contracts;
using Ufw.Roslyn.SourceGen.Controllers.Contracts;

[assembly: GeneratorContractRegistrationAttribute<ControllerGeneratorContract>(ControllerGeneratorContract.ApiControllerMappingTriggerAttribute, typeof(ApiControllerMappingGeneratorAttribute<,,>))]
[assembly: GeneratorContractRegistrationAttribute<ControllerGeneratorContract>(ControllerGeneratorContract.ApiControllerRegistrationAttribute, typeof(ApiControllerRegistrationAttribute<>))]
[assembly: GeneratorContractRegistrationAttribute<ControllerGeneratorContract>(ControllerGeneratorContract.ControllerRouteAttribute, typeof(RouteAttribute))]
[assembly: GeneratorContractRegistrationAttribute<ControllerGeneratorContract>(ControllerGeneratorContract.HttpGetRouteAttribute, typeof(GetAttribute))]
[assembly: GeneratorContractRegistrationAttribute<ControllerGeneratorContract>(ControllerGeneratorContract.HttpPostRouteAttribute, typeof(PostAttribute))]
[assembly: GeneratorContractRegistrationAttribute<ControllerGeneratorContract>(ControllerGeneratorContract.HttpPutRouteAttribute, typeof(PutAttribute))]
[assembly: GeneratorContractRegistrationAttribute<ControllerGeneratorContract>(ControllerGeneratorContract.HttpDeleteRouteAttribute, typeof(DeleteAttribute))]
[assembly: GeneratorContractRegistrationAttribute<ControllerGeneratorContract>(ControllerGeneratorContract.ApiEndpointMapping, typeof(ApiEndpointMapping<,>))]
[assembly: GeneratorContractRegistrationAttribute<ControllerGeneratorContract>(ControllerGeneratorContract.ControllerActivator, typeof(Ufw.Roslyn.Controllers.Internals.Activator))]
[assembly: GeneratorContractRegistrationAttribute<ControllerGeneratorContract>(ControllerGeneratorContract.IdentifiableResponse, typeof(IIdentifiable))]
