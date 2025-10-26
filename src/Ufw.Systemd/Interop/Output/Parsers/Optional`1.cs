namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class Optional<TParser>() : Optional(TParser.Instance), IParser<Optional<TParser>> where TParser : class, IParser<TParser>
{
    public static Optional<TParser> Instance { get; } = new();
}
