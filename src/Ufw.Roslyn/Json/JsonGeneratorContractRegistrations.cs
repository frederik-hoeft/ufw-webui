using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ufw.Roslyn.Json;
using Ufw.Roslyn.SourceGen.Contracts;
using Ufw.Roslyn.SourceGen.Json.Contracts;

[assembly: GeneratorContractRegistrationAttribute<JsonGeneratorContract>(JsonGeneratorContract.JsonTypeInfoBindingsTriggerAttribute, typeof(JsonTypeInfoBindingsGeneratorAttribute))]
[assembly: GeneratorContractRegistrationAttribute<JsonGeneratorContract>(JsonGeneratorContract.AotJsonSerializerContext, typeof(AotJsonSerializerContext))]
[assembly: GeneratorContractRegistrationAttribute<JsonGeneratorContract>(JsonGeneratorContract.JsonSerializableAttribute, typeof(JsonSerializableAttribute))]
[assembly: GeneratorContractRegistrationAttribute<JsonGeneratorContract>(JsonGeneratorContract.JsonTypeInfo, typeof(JsonTypeInfo<>))]
