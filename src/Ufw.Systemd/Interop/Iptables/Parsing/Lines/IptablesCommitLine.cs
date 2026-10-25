using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Lines;

internal sealed class IptablesCommitLine : ParserBase<IIptablesDocumentVisitor>
{
    private readonly IParser _parser;

    private IptablesCommitLine(string? name)
    {
        Name = name;
        _parser = Grammar.Sequence(
            CommitToken.Instance,
            Grammar.Optional(HorizontalWhitespace.Instance),
            EndOfLine.Instance);
    }

    public static IptablesCommitLine Instance { get; } = new(name: null);

    public override string? Name { get; }

    public override IParser NamedCopy(string name) => new IptablesCommitLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_parser.TryParse(input, offset, out ISyntaxNode? node, out charsConsumed))
        {
            syntaxNode = null;
            return false;
        }

        node.Accept(new IptablesTokenCollector());
        syntaxNode = new CommitLineSyntaxNode(Name);
        return true;
    }
}

/// <summary>Matches one literal character and produces no token.</summary>
