using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Iptables.Parsing.Terminals;

namespace Ufw.Systemd.Interop.Iptables.Parsing;

internal sealed class IptablesBlankLine(string? name = null) : ParserBase
{
    public static IptablesBlankLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new IptablesBlankLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        int index = offset;
        while (index < input.Length && input[index] is ' ' or '\t')
        {
            index++;
        }

        if (!RequiredLineEnding.Instance.TryParse(input, index, out _, out int endingLength))
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new IgnoredIptablesLineSyntaxNode(name);
        charsConsumed = index - offset + endingLength;
        return true;
    }
}

internal sealed class IptablesCommentLine(string? name = null) : ParserBase
{
    public static IptablesCommentLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new IptablesCommentLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset >= (uint)input.Length || input[offset] != '#')
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        int index = offset + 1;
        while (index < input.Length && input[index] is not ('\r' or '\n'))
        {
            index++;
        }

        if (RequiredLineEnding.Instance.TryParse(input, index, out _, out int endingLength))
        {
            index += endingLength;
        }

        syntaxNode = new IgnoredIptablesLineSyntaxNode(name);
        charsConsumed = index - offset;
        return true;
    }
}

internal sealed class IptablesTupleLine(string? name = null) : ParserBase<IIptablesDocumentVisitor>, IParser<IptablesTupleLine>
{
    private const string PREFIX = "### tuple ###";

    public static IptablesTupleLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new IptablesTupleLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!TryReadLine(input, offset, out ReadOnlySpan<char> content, out int consumed) || !content.StartsWith(PREFIX))
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        string body = content[PREFIX.Length..].Trim().ToString();
        syntaxNode = new TupleLineSyntaxNode(name, body);
        charsConsumed = consumed;
        return true;
    }

    internal static bool TryReadLine(string input, int offset, out ReadOnlySpan<char> content, out int consumed)
    {
        if ((uint)offset > (uint)input.Length)
        {
            content = default;
            consumed = 0;
            return false;
        }

        int index = offset;
        while (index < input.Length && input[index] is not ('\r' or '\n'))
        {
            index++;
        }

        if (index == offset)
        {
            content = default;
            consumed = 0;
            return false;
        }

        content = input.AsSpan(offset, index - offset);
        if (content[^1] == '\r')
        {
            content = content[..^1];
        }

        if (RequiredLineEnding.Instance.TryParse(input, index, out _, out int endingLength))
        {
            index += endingLength;
        }

        consumed = index - offset;
        return content.Length > 0;
    }
}

internal sealed class IptablesSectionLine(string? name = null) : ParserBase<IIptablesDocumentVisitor>, IParser<IptablesSectionLine>
{
    public static IptablesSectionLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new IptablesSectionLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!IptablesTupleLine.TryReadLine(input, offset, out ReadOnlySpan<char> content, out int consumed))
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        ReadOnlySpan<char> trimmed = content.Trim();
        if (!trimmed.StartsWith("###") || !trimmed.EndsWith("###") || trimmed.Length < 7)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        string section = trimmed[3..^3].Trim().ToString();
        if (section.Length == 0 || section.Contains("###", StringComparison.Ordinal))
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new SectionLineSyntaxNode(name, section);
        charsConsumed = consumed;
        return true;
    }
}

internal sealed class IptablesTableLine : ParserBase<IIptablesDocumentVisitor>
{
    private readonly IParser _parser;

    private IptablesTableLine(string? name)
    {
        Name = name;
        _parser = Grammar.Sequence(
            TableNameToken.Instance.NamedCopy("table-name"),
            Grammar.Optional(HorizontalWhitespace.Instance),
            EndOfLine.Instance);
    }

    public static IptablesTableLine Instance { get; } = new(name: null);

    public override string? Name { get; }

    public override IParser NamedCopy(string name) => new IptablesTableLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_parser.TryParse(input, offset, out ISyntaxNode? node, out charsConsumed))
        {
            syntaxNode = null;
            return false;
        }

        IptablesTokenCollector collector = new();
        node.Accept(collector);
        if (collector.Tokens.Count != 1 || collector.Tokens[0].Kind != IptablesTokenKind.TableName)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new TableLineSyntaxNode(Name, collector.Tokens[0].Text);
        return true;
    }
}

internal sealed class IptablesChainLine : ParserBase<IIptablesDocumentVisitor>
{
    private readonly IParser _parser;

    private IptablesChainLine(string? name)
    {
        Name = name;
        _parser = Grammar.Sequence(
            new LiteralCharacterParser(':'),
            ChainNameToken.Instance.NamedCopy("chain-name"),
            HorizontalWhitespace.Instance,
            ChainPolicyToken.Instance.NamedCopy("chain-policy"),
            HorizontalWhitespace.Instance,
            CounterToken.Instance.NamedCopy("chain-counter"),
            Grammar.Optional(HorizontalWhitespace.Instance),
            EndOfLine.Instance);
    }

    public static IptablesChainLine Instance { get; } = new(name: null);

    public override string? Name { get; }

    public override IParser NamedCopy(string name) => new IptablesChainLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_parser.TryParse(input, offset, out ISyntaxNode? node, out charsConsumed))
        {
            syntaxNode = null;
            return false;
        }

        IptablesTokenCollector collector = new();
        node.Accept(collector);
        string? chainName = null;
        string? policy = null;
        foreach (IptablesToken token in collector.Tokens)
        {
            if (token.Role == "chain-name")
            {
                chainName = token.Text;
            }
            else if (token.Role == "chain-policy")
            {
                policy = token.Text;
            }
        }

        if (chainName is null || policy is null)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new ChainLineSyntaxNode(Name, chainName, policy);
        return true;
    }
}

internal sealed class IptablesRuleLine : ParserBase<IIptablesDocumentVisitor>
{
    private static readonly IParser s_argument = Grammar.Alternative(
        NegationToken.Instance.NamedCopy("rule-argument"),
        LongOptionToken.Instance.NamedCopy("rule-argument"),
        ShortOptionToken.Instance.NamedCopy("rule-argument"),
        QuotedStringToken.Instance.NamedCopy("rule-argument"),
        BareWordToken.Instance.NamedCopy("rule-argument"));

    private readonly IParser _parser;

    private IptablesRuleLine(string? name)
    {
        Name = name;
        _parser = Grammar.Sequence(
            AppendCommand.Instance,
            HorizontalWhitespace.Instance,
            ChainNameToken.Instance.NamedCopy("rule-chain"),
            Grammar.Optional(Grammar.Sequence(HorizontalWhitespace.Instance, CounterToken.Instance.NamedCopy("rule-counter"))),
            Grammar.Repeat(Grammar.Sequence(HorizontalWhitespace.Instance, s_argument)),
            Grammar.Optional(HorizontalWhitespace.Instance),
            EndOfLine.Instance);
    }

    public static IptablesRuleLine Instance { get; } = new(name: null);

    public override string? Name { get; }

    public override IParser NamedCopy(string name) => new IptablesRuleLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_parser.TryParse(input, offset, out ISyntaxNode? node, out charsConsumed))
        {
            syntaxNode = null;
            return false;
        }

        IptablesTokenCollector collector = new();
        node.Accept(collector);
        string? chainName = null;
        List<IptablesToken> arguments = [];
        foreach (IptablesToken token in collector.Tokens)
        {
            if (token.Role == "rule-chain")
            {
                chainName = token.Text;
            }
            else if (token.Role == "rule-argument")
            {
                arguments.Add(token);
            }
        }

        if (chainName is null)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new RuleLineSyntaxNode(Name, chainName, arguments);
        return true;
    }
}

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
internal sealed class LiteralCharacterParser(char character, string? name = null) : ParserBase
{
    public override string? Name => name;

    public override IParser NamedCopy(string name) => new LiteralCharacterParser(character, name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset >= (uint)input.Length || input[offset] != character)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new WhitespaceSyntaxNode(name, 1);
        charsConsumed = 1;
        return true;
    }
}
