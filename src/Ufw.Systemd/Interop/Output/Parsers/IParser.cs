using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal interface IParser
{
    string? Name { get; }

    IParser NamedCopy(string name);

    bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed);
}
