using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ufw.Roslyn.Json;
using Ufw.Roslyn.SourceGen.Contracts;
using Ufw.Roslyn.SourceGen.Json.Contracts;

[assembly: GeneratorContractRegistration<JsonGeneratorContract>(JsonGeneratorContract.JsonTypeInfoBindingsTriggerAttribute, typeof(JsonTypeInfoBindingsGeneratorAttribute))]
[assembly: GeneratorContractRegistration<JsonGeneratorContract>(JsonGeneratorContract.AotJsonSerializerContext, typeof(AotJsonSerializerContext))]
[assembly: GeneratorContractRegistration<JsonGeneratorContract>(JsonGeneratorContract.JsonSerializableAttribute, typeof(JsonSerializableAttribute))]
[assembly: GeneratorContractRegistration<JsonGeneratorContract>(JsonGeneratorContract.JsonTypeInfo, typeof(JsonTypeInfo<>))]
