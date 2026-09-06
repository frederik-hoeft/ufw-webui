using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Firewall;

public sealed class UfwRuleCommandRenderer : IUfwRuleCommandRenderer
{
    public UfwRenderedRule Render(FirewallRuleSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (!TryRender(specification, out UfwRenderedRule? renderedRule))
        {
            throw new InvalidOperationException("Refusing to render UFW syntax from an invalid rule specification.");
        }

        return renderedRule!;
    }

    public bool TryRender(FirewallRuleSpecification specification, [NotNullWhen(true)] out UfwRenderedRule? renderedRule)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (!RuleSpecificationValidator.TryValidate(specification, out _))
        {
            renderedRule = null;
            return false;
        }

        List<string> arguments = [];
        AppendRuleTokens(arguments, RuleSpecificationNormalizer.Normalize(specification));
        ImmutableArray<string> renderedArguments = [.. arguments];
        renderedRule = new UfwRenderedRule(renderedArguments, FormatDisplayText(renderedArguments));
        return true;
    }

    private static void AppendRuleTokens(List<string> arguments, FirewallRuleSpecification specification)
    {
        if (specification.Direction == FirewallDirection.Forward)
        {
            arguments.Add("route");
        }

        arguments.Add(RuleSpecificationNormalizer.FormatAction(specification.Action));
        switch (specification.Direction)
        {
            case FirewallDirection.Forward:
                if (!string.IsNullOrEmpty(specification.SourceInterface))
                {
                    arguments.Add("in");
                    arguments.Add("on");
                    arguments.Add(specification.SourceInterface);
                }

                if (!string.IsNullOrEmpty(specification.DestinationInterface))
                {
                    arguments.Add("out");
                    arguments.Add("on");
                    arguments.Add(specification.DestinationInterface);
                }
                break;
            case FirewallDirection.In:
                arguments.Add("in");
                if (!string.IsNullOrEmpty(specification.DestinationInterface))
                {
                    arguments.Add("on");
                    arguments.Add(specification.DestinationInterface);
                }
                break;
            case FirewallDirection.Out:
                arguments.Add("out");
                if (!string.IsNullOrEmpty(specification.SourceInterface))
                {
                    arguments.Add("on");
                    arguments.Add(specification.SourceInterface);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(specification), specification.Direction, "Unsupported firewall direction.");
        }

        arguments.Add("from");
        arguments.Add(FormatAddressForUfw(specification.Source, specification.AddressFamily));
        if (!string.IsNullOrEmpty(specification.SourcePorts))
        {
            arguments.Add("port");
            arguments.Add(specification.SourcePorts);
        }

        arguments.Add("to");
        arguments.Add(FormatAddressForUfw(specification.Destination, specification.AddressFamily));
        if (!string.IsNullOrEmpty(specification.DestinationPorts))
        {
            arguments.Add("port");
            arguments.Add(specification.DestinationPorts);
        }

        if (specification.Protocol != FirewallProtocol.Any)
        {
            arguments.Add("proto");
            arguments.Add(RuleSpecificationNormalizer.FormatProtocol(specification.Protocol));
        }

        if (!string.IsNullOrEmpty(specification.Comment))
        {
            arguments.Add("comment");
            arguments.Add(specification.Comment);
        }
    }

    private static string FormatAddressForUfw(string? address, FirewallAddressFamily family)
    {
        string normalized = string.IsNullOrWhiteSpace(address) ? RuleSpecificationNormalizer.ANY : address;
        if (!string.Equals(normalized, RuleSpecificationNormalizer.ANY, StringComparison.Ordinal))
        {
            return normalized;
        }

        return family switch
        {
            FirewallAddressFamily.IPv4 => "0.0.0.0/0",
            FirewallAddressFamily.IPv6 => "::/0",
            _ => RuleSpecificationNormalizer.ANY,
        };
    }

    private static string FormatDisplayText(ImmutableArray<string> arguments)
    {
        StringBuilder builder = new();
        for (int index = 0; index < arguments.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            AppendDisplayArgument(builder, arguments[index]);
        }

        return builder.ToString();
    }

    private static void AppendDisplayArgument(StringBuilder builder, string argument)
    {
        if (!argument.Any(static character => char.IsWhiteSpace(character) || character is '"' or '\\'))
        {
            builder.Append(argument);
            return;
        }

        builder.Append('"');
        foreach (char character in argument)
        {
            if (character is '"' or '\\')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }
        builder.Append('"');
    }
}
