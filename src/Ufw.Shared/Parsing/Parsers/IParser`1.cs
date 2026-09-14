namespace Ufw.Shared.Parsing.Parsers;

public interface IParser<TParser> : IParser
    where TParser : class, IParser<TParser>
{
    static abstract TParser Instance { get; }
}
