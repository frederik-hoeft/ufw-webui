using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

/// <summary>
/// Shared scan helpers for iptables terminals. Each terminal parser still recognizes one token.
/// </summary>
internal static class IptablesTerminal
{
    public static bool TryTake(string input, int offset, Func<ReadOnlySpan<char>, int> match, IptablesTokenKind kind, string? name, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        int length = match(input.AsSpan(offset));
        if (length <= 0)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new IptablesTokenSyntaxNode(name, kind, input.Substring(offset, length));
        charsConsumed = length;
        return true;
    }

    public static int MatchWhile(ReadOnlySpan<char> text, Func<char, bool> predicate)
    {
        int index = 0;
        while (index < text.Length && predicate(text[index]))
        {
            index++;
        }

        return index;
    }
}

internal sealed class HorizontalWhitespace(string? name = null) : ParserBase, IParser<HorizontalWhitespace>
{
    public static HorizontalWhitespace Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new HorizontalWhitespace(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        int length = IptablesTerminal.MatchWhile(input.AsSpan(offset), static character => character is ' ' or '\t');
        if (length == 0)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new WhitespaceSyntaxNode(name, length);
        charsConsumed = length;
        return true;
    }
}

/// <summary>Matches a newline, or succeeds with no input only at the end of the text.</summary>
internal sealed class EndOfLine(string? name = null) : ParserBase
{
    public static EndOfLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new EndOfLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (offset == input.Length)
        {
            syntaxNode = new WhitespaceSyntaxNode(name, 0);
            charsConsumed = 0;
            return true;
        }

        int length = 0;
        if (input[offset] == '\r')
        {
            length++;
        }

        if (offset + length >= input.Length || input[offset + length] != '\n')
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new WhitespaceSyntaxNode(name, length + 1);
        charsConsumed = length + 1;
        return true;
    }
}

/// <summary>Matches a newline and nothing else. Fails at end of input so blank lines cannot loop.</summary>
internal sealed class RequiredLineEnding(string? name = null) : ParserBase
{
    public static RequiredLineEnding Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new RequiredLineEnding(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset >= (uint)input.Length)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        int length = 0;
        if (input[offset] == '\r')
        {
            length++;
        }

        if (offset + length >= input.Length || input[offset + length] != '\n')
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new WhitespaceSyntaxNode(name, length + 1);
        charsConsumed = length + 1;
        return true;
    }
}
