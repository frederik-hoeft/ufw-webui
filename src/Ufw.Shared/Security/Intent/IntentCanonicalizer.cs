using System.Globalization;
using System.Text;
using Ufw.Shared.Firewall;

namespace Ufw.Shared.Security.Intent;

/// <summary>
/// Builds the exact byte sequence covered by an intent signature.
/// Field-oriented encoding avoids JSON whitespace/key-order ambiguity.
/// </summary>
public static class IntentCanonicalizer
{
    public static byte[] Canonicalize(ISignedIntent intent, FirewallRuleSpecification rule, string? ruleId = null)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(rule);

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(rule);
        StringBuilder builder = CreateIntentHeader(intent);
        builder.Append("payload:\n");
        if (ruleId is not null)
        {
            AppendField(builder, "ruleId", ruleId);
        }

        AppendRuleFields(builder, normalized);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static byte[] CanonicalizeAdd(ISignedIntent intent, AddRulePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return Canonicalize(intent, payload.Rule);
    }

    public static byte[] CanonicalizeDelete(ISignedIntent intent, DeleteRulePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return Canonicalize(intent, payload.Rule, payload.RuleId);
    }

    public static byte[] CanonicalizeInsert(ISignedIntent intent, InsertRulePayload payload)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(payload.Rule);

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(payload.Rule);
        StringBuilder builder = CreateIntentHeader(intent);
        builder.Append("payload:\n");
        AppendField(builder, "baselineFingerprint", payload.BaselineFingerprint);
        AppendField(builder, "anchorOccurrenceId", payload.AnchorOccurrenceId.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "placement", FormatInsertionPlacement(payload.Placement));
        AppendRuleFields(builder, normalized);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static byte[] CanonicalizeReorder(ISignedIntent intent, ReorderRulesPayload payload)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(payload.DesiredOrder);

        StringBuilder builder = CreateIntentHeader(intent);
        builder.Append("payload:\n");
        AppendField(builder, "baselineFingerprint", payload.BaselineFingerprint);
        AppendField(builder, "desiredOrderCount", payload.DesiredOrder.Length.ToString(CultureInfo.InvariantCulture));
        for (int index = 0; index < payload.DesiredOrder.Length; index++)
        {
            AppendIndexedField(builder, "desiredOrder", index, payload.DesiredOrder[index]);
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendRuleFields(StringBuilder builder, FirewallRuleSpecification normalized)
    {
        AppendField(builder, "action", RuleSpecificationNormalizer.FormatAction(normalized.Action));
        AppendField(builder, "addressFamily", RuleSpecificationNormalizer.FormatAddressFamily(normalized.AddressFamily));
        AppendField(builder, "comment", normalized.Comment ?? string.Empty);
        AppendField(builder, "destination", normalized.Destination ?? RuleSpecificationNormalizer.ANY);
        AppendField(builder, "destinationInterface", normalized.DestinationInterface ?? string.Empty);
        AppendField(builder, "destinationPorts", normalized.DestinationPorts ?? string.Empty);
        AppendField(builder, "direction", RuleSpecificationNormalizer.FormatDirection(normalized.Direction));
        AppendField(builder, "protocol", RuleSpecificationNormalizer.FormatProtocol(normalized.Protocol));
        AppendField(builder, "source", normalized.Source ?? RuleSpecificationNormalizer.ANY);
        AppendField(builder, "sourceInterface", normalized.SourceInterface ?? string.Empty);
        AppendField(builder, "sourcePorts", normalized.SourcePorts ?? string.Empty);
    }

    private static string FormatInsertionPlacement(RuleInsertionPlacement placement) => placement switch
    {
        RuleInsertionPlacement.Before => "before",
        RuleInsertionPlacement.After => "after",
        _ => throw new ArgumentOutOfRangeException(nameof(placement), placement, "Insertion placement is not supported."),
    };

    private static StringBuilder CreateIntentHeader(ISignedIntent intent)
    {
        StringBuilder builder = new();
        builder.Append(IntentProtocol.CONTEXT);
        builder.Append('\n');
        AppendField(builder, "deploymentId", intent.DeploymentId);
        AppendField(builder, "keyId", intent.KeyId);
        AppendField(builder, "issuedAtUnix", intent.IssuedAtUnix.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "nonce", intent.Nonce);
        AppendField(builder, "operation", intent.Operation);
        return builder;
    }

    private static void AppendIndexedField(StringBuilder builder, string name, int index, int value)
    {
        builder.Append(name);
        builder.Append('[');
        builder.Append(index.ToString(CultureInfo.InvariantCulture));
        builder.Append("]=");
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
        builder.Append('\n');
    }

    private static void AppendField(StringBuilder builder, string name, string value)
    {
        builder.Append(name);
        builder.Append('=');
        builder.Append(value);
        builder.Append('\n');
    }
}
