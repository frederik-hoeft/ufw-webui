using System.Text.Json.Serialization;

namespace Ufw.Systemd.Firewall.Ordering;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ReorderRecoveryJournalEntry))]
internal sealed partial class ReorderRecoveryJsonSerializerContext : JsonSerializerContext;
