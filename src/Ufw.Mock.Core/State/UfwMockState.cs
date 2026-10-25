namespace Ufw.Mock.State;

internal sealed class UfwMockState
{
    public const int CURRENT_SCHEMA_VERSION = 1;

    public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

    public bool Enabled { get; set; }

    public string LoggingLevel { get; set; } = "low";

    public string DefaultIncomingPolicy { get; set; } = "deny";

    public string DefaultOutgoingPolicy { get; set; } = "allow";

    public string DefaultRoutedPolicy { get; set; } = "deny";

    public string DefaultApplicationPolicy { get; set; } = "skip";

    public bool IPv6Enabled { get; set; } = true;

    public List<UfwMockRule> Rules { get; set; } = [];

    public List<UfwApplicationProfile> ApplicationProfiles { get; set; } = [];

    public static UfwMockState CreateDefault() => new();
}
