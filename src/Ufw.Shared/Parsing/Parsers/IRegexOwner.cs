using System.Text.RegularExpressions;

namespace Ufw.Shared.Parsing.Parsers;

public interface IRegexOwner
{
    static abstract Regex ParserRegex { get; }
}
