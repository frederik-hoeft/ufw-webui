namespace Ufw.Shared.Parsing.Parsers;

public sealed class Sequence<TParser1, TParser2, TParser3>(string? name = null)
    : Sequence(parsers: [TParser1.Instance, TParser2.Instance, TParser3.Instance], name), IParser<Sequence<TParser1, TParser2, TParser3>>
    where TParser1 : class, IParser<TParser1>
    where TParser2 : class, IParser<TParser2>
    where TParser3 : class, IParser<TParser3>
{
    public static Sequence<TParser1, TParser2, TParser3> Instance { get; } = new();
}
