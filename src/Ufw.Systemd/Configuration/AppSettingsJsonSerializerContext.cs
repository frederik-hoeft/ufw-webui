using System.Text.Json;
using System.Text.Json.Serialization;
using Ufw.Roslyn.Json;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Configuration;

[JsonTypeInfoBindingsGenerator(GenerationMode = BindingsGenerationMode.Optimized)]
[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonSerializerContext : AotJsonSerializerContext;