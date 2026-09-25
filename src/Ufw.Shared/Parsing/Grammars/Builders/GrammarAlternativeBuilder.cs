using Ufw.Shared.Parsing.Parsers;

namespace Ufw.Shared.Parsing.Grammars.Builders;

public sealed class GrammarAlternativeBuilder : GrammarCollectionBuilder
{
    public GrammarAlternativeBuilder Sequence(Action<GrammarSequenceBuilder> buildSequence) => AddSequence(this, buildSequence);

    public GrammarAlternativeBuilder Optional(Action<GrammarOptionalBuilder> buildOptional) => AddOptional(this, buildOptional);

    public GrammarAlternativeBuilder Alternative(Action<GrammarAlternativeBuilder> buildAlternative) => AddAlternative(this, buildAlternative);

    public GrammarAlternativeBuilder Set(Action<GrammarSetBuilder> buildSet) => AddSet(this, buildSet);

    public GrammarAlternativeBuilder Repeat(IParser parser, int minimumCount = 0) => AddParser(this, new Repeat(parser, minimumCount));

    public GrammarAlternativeBuilder Repeat<TParser>() where TParser : class, IParser<TParser> => AddParser(this, Parsers.Repeat<TParser>.Instance);

    public GrammarAlternativeBuilder Parser(IParser parser) => AddParser(this, parser);

    public GrammarAlternativeBuilder Parser<TParser>() where TParser : class, IParser<TParser> => AddParser(this, TParser.Instance);

    public override IParser Build() => new Alternative([.. GetChildrenNonEmpty()]);
}
