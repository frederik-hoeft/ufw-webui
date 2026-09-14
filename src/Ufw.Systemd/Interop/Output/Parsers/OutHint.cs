using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed partial class OutHint(string? name = null) : ParserBase<IUfwListCommandResultRowVisitor>, IParser<OutHint>
{
    public static OutHint Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new OutHint(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        const string OUT = "(out)";
        ReadOnlySpan<char> inputSpan = input.AsSpan(offset);
        if (inputSpan.StartsWith(OUT, StringComparison.Ordinal))
        {
            charsConsumed = OUT.Length;
            syntaxNode = new OutSyntaxNode(Name);
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}
