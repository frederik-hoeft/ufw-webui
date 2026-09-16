namespace Ufw.Client.Rules.Filtering;

internal sealed record FieldRuleMatchEvidence(FieldRuleMatchEvidence.FieldKind Field, string Value) : RuleMatchEvidence
{
    internal enum FieldKind
    {
        Protocol,
        Action,
        Direction,
    }
}
