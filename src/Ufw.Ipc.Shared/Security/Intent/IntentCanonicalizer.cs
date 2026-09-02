using System.Globalization;
using System.Text;
using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Ipc.Shared.Security.Intent;

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
        StringBuilder builder = new();
        builder.Append(IntentProtocol.CONTEXT);
        builder.Append('\n');
        AppendField(builder, "deploymentId", intent.DeploymentId);
        AppendField(builder, "keyId", intent.KeyId);
        AppendField(builder, "issuedAtUnix", intent.IssuedAtUnix.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "nonce", intent.Nonce);
        AppendField(builder, "operation", intent.Operation);
        builder.Append("payload:\n");
        if (ruleId is not null)
        {
            AppendField(builder, "ruleId", ruleId);
        }

        AppendField(builder, "action", RuleSpecificationNormalizer.FormatAction(normalized.Action));
        AppendField(builder, "comment", normalized.Comment ?? string.Empty);
        AppendField(builder, "destination", normalized.Destination ?? RuleSpecificationNormalizer.ANY);
        AppendField(builder, "destinationInterface", normalized.DestinationInterface ?? string.Empty);
        AppendField(builder, "destinationPorts", normalized.DestinationPorts ?? string.Empty);
        AppendField(builder, "direction", RuleSpecificationNormalizer.FormatDirection(normalized.Direction));
        AppendField(builder, "protocol", RuleSpecificationNormalizer.FormatProtocol(normalized.Protocol));
        AppendField(builder, "source", normalized.Source ?? RuleSpecificationNormalizer.ANY);
        AppendField(builder, "sourceInterface", normalized.SourceInterface ?? string.Empty);
        AppendField(builder, "sourcePorts", normalized.SourcePorts ?? string.Empty);
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

    private static void AppendField(StringBuilder builder, string name, string value)
    {
        builder.Append(name);
        builder.Append('=');
        builder.Append(value);
        builder.Append('\n');
    }
}
