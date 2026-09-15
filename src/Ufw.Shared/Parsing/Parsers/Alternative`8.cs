namespace Ufw.Shared.Parsing.Parsers;

public sealed class Alternative<TParser1, TParser2, TParser3, TParser4, TParser5, TParser6, TParser7, TParser8>(string? name = null)
    : Alternative(
        parsers: [TParser1.Instance, TParser2.Instance, TParser3.Instance, TParser4.Instance, TParser5.Instance, TParser6.Instance, TParser7.Instance, TParser8.Instance],
        name),
      IParser<Alternative<TParser1, TParser2, TParser3, TParser4, TParser5, TParser6, TParser7, TParser8>>
    where TParser1 : class, IParser<TParser1>
    where TParser2 : class, IParser<TParser2>
    where TParser3 : class, IParser<TParser3>
    where TParser4 : class, IParser<TParser4>
    where TParser5 : class, IParser<TParser5>
    where TParser6 : class, IParser<TParser6>
    where TParser7 : class, IParser<TParser7>
    where TParser8 : class, IParser<TParser8>
{
    public static Alternative<TParser1, TParser2, TParser3, TParser4, TParser5, TParser6, TParser7, TParser8> Instance { get; } = new();
}
