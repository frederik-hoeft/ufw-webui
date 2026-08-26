using Ufw.Systemd.Interop.Output.Parsers;

namespace Ufw.Systemd.Interop.Output.Grammars.Builders;

internal sealed class GrammarSequenceBuilder : GrammarCollectionBuilder
{
    public GrammarSequenceBuilder Sequence(Action<GrammarSequenceBuilder> buildSequence) => AddSequence(this, buildSequence);

    public GrammarSequenceBuilder Optional(Action<GrammarOptionalBuilder> buildOptional) => AddOptional(this, buildOptional);

    public GrammarSequenceBuilder Alternative(Action<GrammarAlternativeBuilder> buildAlternative) => AddAlternative(this, buildAlternative);

    public GrammarSequenceBuilder Parser(IParser parser) => AddParser(this, parser);

    public GrammarSequenceBuilder Parser<TParser>() where TParser : class, IParser<TParser> => AddParser(this, TParser.Instance);

    public override IParser Build() => new Sequence([.. GetChildrenNonEmpty()]);
}
