using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Anywhere(string? name = null) : ParserBase<IUfwListCommandResultRowVisitor>, IParser<Anywhere>
{
    public static Anywhere Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new Anywhere(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        const string ANYWHERE = "Anywhere";
        ReadOnlySpan<char> inputSpan = input.AsSpan(offset);
        if (inputSpan.StartsWith(ANYWHERE, StringComparison.Ordinal))
        {
            charsConsumed = ANYWHERE.Length;
            syntaxNode = new AnywhereSyntaxNode(Name);
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}
