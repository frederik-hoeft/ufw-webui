namespace Ufw.Shared.Parsing.Parsers;

public sealed class Sequence<TParser1, TParser2>(string? name = null)
    : Sequence(parsers: [TParser1.Instance, TParser2.Instance], name), IParser<Sequence<TParser1, TParser2>>
    where TParser1 : class, IParser<TParser1>
    where TParser2 : class, IParser<TParser2>
{
    public static Sequence<TParser1, TParser2> Instance { get; } = new();
}
