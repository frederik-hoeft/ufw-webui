namespace Ufw.Shared.Parsing.Parsers;

public sealed class Repeat<TParser>(string? name = null) : Repeat(TParser.Instance, name: name), IParser<Repeat<TParser>>
    where TParser : class, IParser<TParser>
{
    public static Repeat<TParser> Instance { get; } = new();
}
