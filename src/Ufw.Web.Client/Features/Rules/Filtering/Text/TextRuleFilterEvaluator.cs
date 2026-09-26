using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering.KnownHosts;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Filtering.Text;

internal sealed class TextRuleFilterEvaluator(IRuleKnownHostProjectionService knownHostProjection) : RuleFilterEvaluator<TextRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, TextRuleFilter filter, RuleFilterContext context)
    {
        IReadOnlyList<SearchField> fields = CreateFields(row, context);
        List<RuleMatchEvidence> evidence = [];
        foreach (string term in filter.Terms)
        {
            TextRuleMatchEvidence? match = FindFirst(fields, term);
            if (match is null)
            {
                return RuleMatchEvaluation.NoMatch;
            }
            evidence.Add(match);
        }
        return new RuleMatchEvaluation(true, evidence);
    }

    private IReadOnlyList<SearchField> CreateFields(RuleRowProjection row, RuleFilterContext context)
    {
        List<SearchField> fields = [];
        FirewallRuleSpecification? rule = row.Rule.Rule;
        if (row.Rule.Parsed && rule is not null)
        {
            Add(fields, TextRuleMatchEvidence.FieldKind.Comment, rule.Comment);
            Add(fields, TextRuleMatchEvidence.FieldKind.Source, rule.Source);
            Add(fields, TextRuleMatchEvidence.FieldKind.SourcePorts, rule.SourcePorts);
            Add(fields, TextRuleMatchEvidence.FieldKind.SourceInterface, rule.SourceInterface);
            Add(fields, TextRuleMatchEvidence.FieldKind.Destination, rule.Destination);
            Add(fields, TextRuleMatchEvidence.FieldKind.DestinationPorts, rule.DestinationPorts);
            Add(fields, TextRuleMatchEvidence.FieldKind.DestinationInterface, rule.DestinationInterface);
            Add(fields, TextRuleMatchEvidence.FieldKind.Action, RuleSpecificationNormalizer.FormatAction(rule.Action));
            Add(fields, TextRuleMatchEvidence.FieldKind.Direction, RuleSpecificationNormalizer.FormatDirection(rule.Direction));
            Add(fields, TextRuleMatchEvidence.FieldKind.Protocol, RuleSpecificationNormalizer.FormatProtocol(rule.Protocol));
        }
        Add(fields, TextRuleMatchEvidence.FieldKind.Notes, row.Metadata?.Notes);
        if (row.Metadata is { } metadata)
        {
            foreach (RuleTag tag in metadata.Tags)
            {
                Add(fields, TextRuleMatchEvidence.FieldKind.Tag, tag.Name);
            }
            if (metadata.Group is { } group)
            {
                Add(fields, TextRuleMatchEvidence.FieldKind.GroupName, group.Name);
                Add(fields, TextRuleMatchEvidence.FieldKind.GroupComment, group.Comment);
            }
        }
        Add(fields, TextRuleMatchEvidence.FieldKind.CanonicalCommand, row.CanonicalCommand);
        foreach (RuleKnownHostProjection projection in knownHostProjection.Project(row, context))
        {
            TextRuleMatchEvidence.FieldKind field = projection.Endpoint == RuleEndpointField.Source
                ? TextRuleMatchEvidence.FieldKind.SourceKnownHost
                : TextRuleMatchEvidence.FieldKind.DestinationKnownHost;
            Add(fields, field, FormatKnownHost(projection));
        }
        Add(fields, TextRuleMatchEvidence.FieldKind.RawLine, row.Rule.RawLine);
        return fields;
    }

    private static string FormatKnownHost(RuleKnownHostProjection projection)
    {
        string value = $"{projection.Host.Name} [{projection.Host.Address}]";
        return string.IsNullOrWhiteSpace(projection.Host.Comment)
            ? value
            : $"{value} {projection.Host.Comment}";
    }

    private static void Add(List<SearchField> fields, TextRuleMatchEvidence.FieldKind kind, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new SearchField(kind, value));
        }
    }

    private static TextRuleMatchEvidence? FindFirst(IReadOnlyList<SearchField> fields, string term)
    {
        foreach (SearchField field in fields)
        {
            int start = field.Value.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
            {
                return new TextRuleMatchEvidence(field.Kind, term, field.Value, start, term.Length);
            }
        }
        return null;
    }

    private sealed record SearchField(TextRuleMatchEvidence.FieldKind Kind, string Value);
}
