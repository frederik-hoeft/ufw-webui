namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Sequence<T1, T2, T3>(string? name = null) : Sequence
(parsers: [T1.Instance, T2.Instance, T3.Instance], name), IParser<Sequence<T1, T2, T3>> where T1 : class, IParser<T1> where T2 : class, IParser<T2> where T3 : class, IParser<T3>
{
    public static Sequence<T1, T2, T3> Instance { get; } = new();
}
