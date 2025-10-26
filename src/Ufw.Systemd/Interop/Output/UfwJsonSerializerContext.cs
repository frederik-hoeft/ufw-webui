using System.Text.Json.Serialization;
using Ufw.Roslyn.Json;
using Ufw.Systemd.Interop.Output.Model;

namespace Ufw.Systemd.Interop.Output;

[JsonTypeInfoBindingsGenerator(GenerationMode = BindingsGenerationMode.Optimized)]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(UfwRuleContext))]
internal sealed partial class UfwJsonSerializerContext : AotJsonSerializerContext;