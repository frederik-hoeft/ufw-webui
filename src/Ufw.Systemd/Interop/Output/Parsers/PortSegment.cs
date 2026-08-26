using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed partial class PortSegment(string? name = null) : RegexParserBase<PortSegment>(name), IParser<PortSegment>, IRegexOwner
{
    public static PortSegment Instance { get; } = new();

    [GeneratedRegex(@"\G(?<ports>(?<port_0>[1-9][0-9]{0,4}(?<range_0>:[1-9][0-9]{0,4})?(?![0-9\.]))(,(?<port_n>[1-9][0-9]{0,4}(?<range_n>:[1-9][0-9]{0,4})?))*)")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new PortSegment(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string ports = match.Groups["ports"].Value;
        ReadOnlySpan<char> portsSpan = ports.AsSpan();
        foreach (Range range in portsSpan.Split(','))
        {
            ReadOnlySpan<char> portOrRange = portsSpan[range];
            int colonIndex = portOrRange.IndexOf(':');
            if (colonIndex != -1)
            {
                Debug.Assert(portOrRange.LastIndexOf(':') == colonIndex, "There should be only one colon in a port range.");
                ReadOnlySpan<char> startPort = portOrRange[..colonIndex];
                ReadOnlySpan<char> endPort = portOrRange[(colonIndex + 1)..];
                if (!ValidatePort(startPort) || !ValidatePort(endPort))
                {
                    goto FAILURE;
                }
            }
            else if (!ValidatePort(portOrRange))
            {
                goto FAILURE;
            }
        }
        syntaxNode = new PortSyntaxNode(Name, ports);
        return true;
    FAILURE:
        syntaxNode = null;
        return false;
    }

    private static bool ValidatePort(ReadOnlySpan<char> port) => int.TryParse(port, out int portNumber) && portNumber is >= 1 and <= 65535;
}