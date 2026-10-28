namespace Ufw.Shared.Firewall.Iptables.Parsing;

/// <summary>Lexical categories recognized in iptables and UFW rule documents.</summary>
public enum IptablesTokenKind
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
