using Ufw.Shared.Parsing.Parsers;

namespace Ufw.Shared.Parsing.Grammars.Builders;

public sealed class GrammarSequenceBuilder : GrammarCollectionBuilder
{
    public GrammarSequenceBuilder Sequence(Action<GrammarSequenceBuilder> buildSequence) => AddSequence(this, buildSequence);

    public GrammarSequenceBuilder Optional(Action<GrammarOptionalBuilder> buildOptional) => AddOptional(this, buildOptional);

    public GrammarSequenceBuilder Alternative(Action<GrammarAlternativeBuilder> buildAlternative) => AddAlternative(this, buildAlternative);

    public GrammarSequenceBuilder Set(Action<GrammarSetBuilder> buildSet) => AddSet(this, buildSet);

    public GrammarSequenceBuilder Repeat(IParser parser, int minimumCount = 0) => AddParser(this, new Repeat(parser, minimumCount));

    public GrammarSequenceBuilder Repeat<TParser>() where TParser : class, IParser<TParser> => AddParser(this, Parsers.Repeat<TParser>.Instance);

    public GrammarSequenceBuilder Parser(IParser parser) => AddParser(this, parser);

    public GrammarSequenceBuilder Parser<TParser>() where TParser : class, IParser<TParser> => AddParser(this, TParser.Instance);

    public override IParser Build() => new Sequence([.. GetChildrenNonEmpty()]);
}
