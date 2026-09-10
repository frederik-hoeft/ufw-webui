namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Alternative<T1, T2, T3, T4, T5, T6>(string? name = null) : Alternative
(
    parsers: [T1.Instance, T2.Instance, T3.Instance, T4.Instance, T5.Instance, T6.Instance],
    name
), IParser<Alternative<T1, T2, T3, T4, T5, T6>> where T1 : class, IParser<T1> where T2 : class, IParser<T2> where T3 : class, IParser<T3> where T4 : class, IParser<T4>
    where T5 : class, IParser<T5> where T6 : class, IParser<T6>
{
    public static Alternative<T1, T2, T3, T4, T5, T6> Instance { get; } = new();
}
