using System.Collections.Immutable;
using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Systemd.Firewall;

/// <summary>
/// Builds argv arrays for UFW. Values are only emitted after validation so they
/// cannot be interpreted as additional options or a shell command.
/// </summary>
internal static class UfwRuleArgumentBuilder
{
    public static ImmutableArray<string> BuildAdd(FirewallRuleSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (!RuleSpecificationValidator.TryValidate(specification, out _))
        {
            throw new InvalidOperationException("Refusing to build UFW arguments from an invalid rule specification.");
        }

        List<string> arguments = ["--force"];
        AppendRuleTokens(arguments, RuleSpecificationNormalizer.Normalize(specification));
        return [.. arguments];
    }

    public static ImmutableArray<string> BuildDeleteByNumber(int displayNumber)
    {
        if (displayNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayNumber), displayNumber, "UFW rule numbers are 1-based.");
        }

        return ["--force", "delete", displayNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)];
    }

    public static ImmutableArray<string> BuildDeleteBySpecification(FirewallRuleSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (!RuleSpecificationValidator.TryValidate(specification, out _))
        {
            throw new InvalidOperationException("Refusing to build UFW arguments from an invalid rule specification.");
        }

        List<string> arguments = ["--force", "delete"];
        AppendRuleTokens(arguments, RuleSpecificationNormalizer.Normalize(specification));
        return [.. arguments];
    }

    public static ImmutableArray<string> BuildList() => ["status", "numbered"];

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
}
