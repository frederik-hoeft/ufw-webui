namespace Ufw.Systemd.Interop.Iptables.Parsing;

internal enum IptablesTokenKind
{
    TableName,
    ChainName,
    Policy,
    Counter,
    Append,
    Negation,
    ShortOption,
    LongOption,
    QuotedString,
    BareWord,
    Commit,
    TupleBody,
    SectionName,
}
