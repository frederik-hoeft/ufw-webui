using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

internal sealed class AppendCommand(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<AppendCommand>
{
    public static AppendCommand Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new AppendCommand(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => text.StartsWith("-A") && (text.Length == 2 || !IsNameCharacter(text[2])) ? 2 : 0, IptablesTokenKind.Append, name, out syntaxNode, out charsConsumed);
    }

    private static bool IsNameCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '_' or '-';
}

internal sealed class ChainNameToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<ChainNameToken>
{
    public static ChainNameToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new ChainNameToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => IptablesTerminal.MatchWhile(text, static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'), IptablesTokenKind.ChainName, name, out syntaxNode, out charsConsumed);
    }
}

internal sealed class ChainPolicyToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<ChainPolicyToken>
{
    public static ChainPolicyToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new ChainPolicyToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text =>
        {
            if (text.StartsWith("ACCEPT"))
            {
                return 6;
            }

            if (text.StartsWith("DROP"))
            {
                return 4;
            }

            if (text.Length > 0 && text[0] == '-' && (text.Length == 1 || !IsPolicyContinuation(text[1])))
            {
                return 1;
            }

            return 0;
        }, IptablesTokenKind.Policy, name, out syntaxNode, out charsConsumed);
    }

    private static bool IsPolicyContinuation(char character) => char.IsAsciiLetterOrDigit(character) || character is '_' or '-';
}

internal sealed class CounterToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<CounterToken>
{
    public static CounterToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new CounterToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text =>
        {
            if (text.Length < 5 || text[0] != '[')
            {
                return 0;
            }

            int index = 1;
            int digits = IptablesTerminal.MatchWhile(text[index..], static character => char.IsAsciiDigit(character));
            if (digits == 0)
            {
                return 0;
            }

            index += digits;
            if (index >= text.Length || text[index] != ':')
            {
                return 0;
            }

            index++;
            digits = IptablesTerminal.MatchWhile(text[index..], static character => char.IsAsciiDigit(character));
            if (digits == 0)
            {
                return 0;
            }

            index += digits;
            if (index >= text.Length || text[index] != ']')
            {
                return 0;
            }

            return index + 1;
        }, IptablesTokenKind.Counter, name, out syntaxNode, out charsConsumed);
    }
}

internal sealed class NegationToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<NegationToken>
{
    public static NegationToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new NegationToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => text.Length > 0 && text[0] == '!' ? 1 : 0, IptablesTokenKind.Negation, name, out syntaxNode, out charsConsumed);
    }
}

internal sealed class LongOptionToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<LongOptionToken>
{
    public static LongOptionToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new LongOptionToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text =>
        {
            if (text.Length < 3 || text[0] != '-' || text[1] != '-')
            {
                return 0;
            }

            int length = 2 + IptablesTerminal.MatchWhile(text[2..], static character => char.IsAsciiLetterOrDigit(character) || character == '-');
            return length > 2 ? length : 0;
        }, IptablesTokenKind.LongOption, name, out syntaxNode, out charsConsumed);
    }
}

internal sealed class ShortOptionToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<ShortOptionToken>
{
    public static ShortOptionToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new ShortOptionToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => text.Length >= 2 && text[0] == '-' && char.IsAsciiLetterOrDigit(text[1]) ? 2 : 0, IptablesTokenKind.ShortOption, name, out syntaxNode, out charsConsumed);
    }
}

internal sealed class QuotedStringToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<QuotedStringToken>
{
    public static QuotedStringToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new QuotedStringToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset >= (uint)input.Length || input[offset] is not ('"' or '\''))
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        char quote = input[offset];
        int index = offset + 1;
        System.Text.StringBuilder value = new();
        while (index < input.Length && input[index] != quote)
        {
            if (input[index] == '\\' && index + 1 < input.Length)
            {
                value.Append(input[index + 1]);
                index += 2;
                continue;
            }

            if (input[index] is '\r' or '\n')
            {
                syntaxNode = null;
                charsConsumed = 0;
                return false;
            }

            value.Append(input[index]);
            index++;
        }

        if (index >= input.Length || input[index] != quote)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new SyntaxNodes.IptablesTokenSyntaxNode(name, IptablesTokenKind.QuotedString, value.ToString());
        charsConsumed = index + 1 - offset;
        return true;
    }
}

internal sealed class BareWordToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<BareWordToken>
{
    public static BareWordToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new BareWordToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text =>
        {
            if (text.Length == 0 || !IsBareStart(text[0]))
            {
                return 0;
            }

            return IptablesTerminal.MatchWhile(text, static character => !char.IsWhiteSpace(character));
        }, IptablesTokenKind.BareWord, name, out syntaxNode, out charsConsumed);
    }

    private static bool IsBareStart(char character) => !char.IsWhiteSpace(character) && character is not ('-' or '!' or '"' or '\'' or '#');
}

internal sealed class TableNameToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<TableNameToken>
{
    public static TableNameToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new TableNameToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset >= (uint)input.Length || input[offset] != '*')
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        int length = IptablesTerminal.MatchWhile(input.AsSpan(offset + 1), static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
        if (length == 0)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new SyntaxNodes.IptablesTokenSyntaxNode(name, IptablesTokenKind.TableName, input.Substring(offset + 1, length));
        charsConsumed = length + 1;
        return true;
    }
}

internal sealed class CommitToken(string? name = null) : ParserBase<IIptablesTokenVisitor>, IParser<CommitToken>
{
    public static CommitToken Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new CommitToken(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IptablesTerminal.TryTake(input, offset, static text => text.StartsWith("COMMIT") && (text.Length == 6 || !char.IsAsciiLetterOrDigit(text[6])) ? 6 : 0, IptablesTokenKind.Commit, name, out syntaxNode, out charsConsumed);
    }
}
