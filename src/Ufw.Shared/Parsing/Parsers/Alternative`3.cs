namespace Ufw.Shared.Parsing.Parsers;

public sealed class Alternative<TParser1, TParser2, TParser3>(string? name = null)
    : Alternative(parsers: [TParser1.Instance, TParser2.Instance, TParser3.Instance], name), IParser<Alternative<TParser1, TParser2, TParser3>>
    where TParser1 : class, IParser<TParser1>
    where TParser2 : class, IParser<TParser2>
    where TParser3 : class, IParser<TParser3>
{
    public static Alternative<TParser1, TParser2, TParser3> Instance { get; } = new();
}
