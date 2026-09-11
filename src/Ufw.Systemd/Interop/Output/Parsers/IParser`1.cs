namespace Ufw.Systemd.Interop.Output.Parsers;

internal interface IParser<TParser> : IParser
    where TParser : class, IParser<TParser>
{
    static abstract TParser Instance { get; }
}
