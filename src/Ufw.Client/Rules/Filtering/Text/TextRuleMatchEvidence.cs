namespace Ufw.Client.Rules.Filtering.Text;

internal sealed record TextRuleMatchEvidence(TextRuleMatchEvidence.FieldKind Field, string Term, string Value, int Start, int Length) : RuleMatchEvidence
{
    internal enum FieldKind
    {
        Comment,
        Source,
        SourcePorts,
        SourceInterface,
        Destination,
        DestinationPorts,
        DestinationInterface,
        Action,
        Direction,
        Protocol,
        Notes,
        Tag,
        CanonicalCommand,
        SourceKnownHost,
        DestinationKnownHost,
        RawLine,
    }
}
