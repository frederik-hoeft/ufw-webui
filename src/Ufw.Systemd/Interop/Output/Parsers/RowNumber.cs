using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed partial class RowNumber(string? name = null) : RegexParserBase<RowNumber>(name), IParser<RowNumber>, IRegexOwner
{
    public static RowNumber Instance { get; } = new();

    [GeneratedRegex(@"\G\[\s*(?<row_number>[1-9][0-9]*)\]")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string name) => new RowNumber(name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string rowNumberText = match.Groups["row_number"].Value;
        if (int.TryParse(rowNumberText, out int rowNumber))
        {
            syntaxNode = new RowNumberSyntaxNode(Name, rowNumber);
            return true;
        }
        syntaxNode = null;
        return false;
    }
}