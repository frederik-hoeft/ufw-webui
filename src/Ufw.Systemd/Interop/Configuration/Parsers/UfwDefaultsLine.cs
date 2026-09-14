using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Configuration.SyntaxNodes;
using Ufw.Systemd.Interop.Configuration.Visitors;

namespace Ufw.Systemd.Interop.Configuration.Parsers;

internal sealed class UfwDefaultsLine(string? name = null) : ParserBase<IUfwDefaultsVisitor>, IParser<UfwDefaultsLine>
{
    public static UfwDefaultsLine Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new UfwDefaultsLine(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset >= (uint)input.Length)
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        ReadOnlySpan<char> remaining = input.AsSpan(offset);
        int lineLength = remaining.IndexOf('\n');
        if (lineLength < 0)
        {
            lineLength = remaining.Length;
            charsConsumed = lineLength;
        }
        else
        {
            charsConsumed = lineLength + 1;
        }

        ReadOnlySpan<char> line = remaining[..lineLength];
        if (!line.IsEmpty && line[^1] == '\r')
        {
            line = line[..^1];
        }

        if (TryParseAssignment(line, out UfwDefaultsAssignment assignment))
        {
            syntaxNode = new UfwDefaultsAssignmentSyntaxNode(Name, assignment);
            return true;
        }

        syntaxNode = new IgnoredUfwDefaultsLineSyntaxNode(Name);
        return true;
    }

    private static bool TryParseAssignment(ReadOnlySpan<char> line, out UfwDefaultsAssignment assignment)
    {
        ReadOnlySpan<char> candidate = line.Trim();
        if (candidate.IsEmpty || candidate[0] == '#')
        {
            assignment = default;
            return false;
        }

        int index = 0;
        while (index < candidate.Length && IsIdentifierCharacter(candidate[index]))
        {
            index++;
        }
        if (index == 0)
        {
            assignment = default;
            return false;
        }

        string key = candidate[..index].ToString();
        while (index < candidate.Length && char.IsWhiteSpace(candidate[index]))
        {
            index++;
        }
        if (index >= candidate.Length || candidate[index] != '=')
        {
            assignment = default;
            return false;
        }
        index++;

        ReadOnlySpan<char> rawValue = candidate[index..].Trim();
        if (!TryParseValue(rawValue, out string? value))
        {
            assignment = new UfwDefaultsAssignment(key, Value: null, IsValid: false);
            return true;
        }

        assignment = new UfwDefaultsAssignment(key, value, IsValid: true);
        return true;
    }

    private static bool TryParseValue(ReadOnlySpan<char> rawValue, [NotNullWhen(true)] out string? value)
    {
        if (rawValue.IsEmpty)
        {
            value = null;
            return false;
        }

        if (rawValue[0] is '\'' or '"')
        {
            char quote = rawValue[0];
            int closingQuote = rawValue[1..].IndexOf(quote);
            if (closingQuote < 0)
            {
                value = null;
                return false;
            }
            closingQuote++;

            ReadOnlySpan<char> trailing = rawValue[(closingQuote + 1)..].TrimStart();
            if (!trailing.IsEmpty && trailing[0] != '#')
            {
                value = null;
                return false;
            }

            ReadOnlySpan<char> quotedValue = rawValue[1..closingQuote];
            if (quotedValue.IsEmpty)
            {
                value = null;
                return false;
            }

            value = quotedValue.ToString();
            return true;
        }

        int commentIndex = rawValue.IndexOf('#');
        ReadOnlySpan<char> unquotedValue = (commentIndex < 0 ? rawValue : rawValue[..commentIndex]).Trim();
        if (unquotedValue.IsEmpty)
        {
            value = null;
            return false;
        }

        value = unquotedValue.ToString();
        return true;
    }

    private static bool IsIdentifierCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character == '_';
}
