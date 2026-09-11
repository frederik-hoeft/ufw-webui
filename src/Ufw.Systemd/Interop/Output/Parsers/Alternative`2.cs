namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Alternative<T1, T2>(string? name = null) : Alternative
(parsers: [T1.Instance, T2.Instance], name), IParser<Alternative<T1, T2>> where T1 : class, IParser<T1> where T2 : class, IParser<T2>
{
    public static Alternative<T1, T2> Instance { get; } = new();
}
