using System.Text.RegularExpressions;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal interface IRegexOwner
{
    static abstract Regex ParserRegex { get; }
}
