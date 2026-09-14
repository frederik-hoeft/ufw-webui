namespace Ufw.Shared.Parsing.Parsers;

public sealed class Alternative<TParser1, TParser2>(string? name = null)
    : Alternative(parsers: [TParser1.Instance, TParser2.Instance], name), IParser<Alternative<TParser1, TParser2>>
    where TParser1 : class, IParser<TParser1>
    where TParser2 : class, IParser<TParser2>
{
    public static Alternative<TParser1, TParser2> Instance { get; } = new();
}
