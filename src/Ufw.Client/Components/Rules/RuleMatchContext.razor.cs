using Microsoft.AspNetCore.Components;
using Ufw.Client.Rules.Filtering;
using Ufw.Client.Rules.Filtering.Actions;
using Ufw.Client.Rules.Filtering.Directions;
using Ufw.Client.Rules.Filtering.Networks;
using Ufw.Client.Rules.Filtering.Ports;
using Ufw.Client.Rules.Filtering.Protocols;
using Ufw.Client.Rules.Filtering.Text;

namespace Ufw.Client.Components.Rules;

public sealed partial class RuleMatchContext
{
    [Parameter, EditorRequired]
    public IReadOnlyList<RuleMatchEvidence> Evidence { get; set; } = [];

    private string DescribeEvidence(RuleMatchEvidence evidence) => evidence switch
    {
        ActionRuleMatchEvidence action => $"{RulesText["ActionColumn"]}: {action.Value}",
        DirectionRuleMatchEvidence direction => $"{RulesText["DirectionColumn"]}: {direction.Value}",
        ProtocolRuleMatchEvidence protocol => $"{RulesText["ProtocolColumn"]}: {protocol.Value}",
        NetworkRuleMatchEvidence network => $"{DescribeEndpoint(network.Endpoint)}: {network.RuleNetwork}",
        PortRuleMatchEvidence ports => $"{DescribeEndpoint(ports.Endpoint)} {RulesText["PortFilter"]}: {ports.RulePorts}",
        _ => evidence.GetType().Name,
    };

    private string DescribeEndpoint(RuleEndpointField endpoint) => endpoint switch
    {
        RuleEndpointField.Source => RulesText["FromColumn"],
        RuleEndpointField.Destination => RulesText["ToColumn"],
        _ => RulesText["RuleEndpointAny"],
    };

    private string DescribeTextField(TextRuleMatchEvidence.FieldKind field) => field switch
    {
        TextRuleMatchEvidence.FieldKind.Comment => RulesText["Comment"],
        TextRuleMatchEvidence.FieldKind.Source => RulesText["Source"],
        TextRuleMatchEvidence.FieldKind.SourcePorts => RulesText["SourcePorts"],
        TextRuleMatchEvidence.FieldKind.SourceInterface => RulesText["SourceInterface"],
        TextRuleMatchEvidence.FieldKind.Destination => RulesText["Destination"],
        TextRuleMatchEvidence.FieldKind.DestinationPorts => RulesText["DestinationPorts"],
        TextRuleMatchEvidence.FieldKind.DestinationInterface => RulesText["DestinationInterface"],
        TextRuleMatchEvidence.FieldKind.Action => RulesText["Action"],
        TextRuleMatchEvidence.FieldKind.Direction => RulesText["Direction"],
        TextRuleMatchEvidence.FieldKind.Protocol => RulesText["Protocol"],
        TextRuleMatchEvidence.FieldKind.RawLine => RulesText["RawRule"],
        _ => field.ToString(),
    };
}
