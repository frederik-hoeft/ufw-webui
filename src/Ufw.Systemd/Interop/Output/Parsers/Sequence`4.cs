namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Sequence<T1, T2, T3, T4>(string? name = null) : Sequence
(
    parsers: [T1.Instance, T2.Instance, T3.Instance, T4.Instance],
    name
), IParser<Sequence<T1, T2, T3, T4>> where T1 : class, IParser<T1> where T2 : class, IParser<T2> where T3 : class, IParser<T3> where T4 : class, IParser<T4>
{
    public static Sequence<T1, T2, T3, T4> Instance { get; } = new();
}
