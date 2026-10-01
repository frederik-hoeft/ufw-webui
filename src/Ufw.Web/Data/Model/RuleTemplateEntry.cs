using Ufw.Shared.Firewall;

namespace Ufw.Web.Data.Model;

internal sealed partial class RuleTemplateEntry
{
    public const int MAX_NAME_LENGTH = Ufw.Web.Model.V1.RuleTemplates.RuleTemplateLimits.MAX_NAME_LENGTH;
    public const int MAX_DESCRIPTION_LENGTH = Ufw.Web.Model.V1.RuleTemplates.RuleTemplateLimits.MAX_DESCRIPTION_LENGTH;

    public long Id { get; set; }

    public Guid PublicId { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Description { get; set; }

    public FirewallAction Action { get; set; }

    public FirewallAddressFamily AddressFamily { get; set; }

    public FirewallDirection Direction { get; set; }

    public FirewallProtocol Protocol { get; set; }

    public required string Source { get; set; }

    public string? SourcePorts { get; set; }

    public string? SourceInterface { get; set; }

    public required string Destination { get; set; }

    public string? DestinationPorts { get; set; }

    public string? DestinationInterface { get; set; }

    public string? Comment { get; set; }

    public string? Notes { get; set; }

    public long? GroupId { get; set; }

    public RuleGroupEntry? Group { get; set; }

    public ICollection<RuleTemplateTagEntry> Tags { get; set; } = [];
}
