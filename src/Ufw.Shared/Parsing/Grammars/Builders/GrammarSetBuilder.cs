using Ufw.Shared.Parsing.Parsers;

namespace Ufw.Shared.Parsing.Grammars.Builders;

public sealed class GrammarSetBuilder : GrammarCollectionBuilder
{
    public GrammarSetBuilder Sequence(Action<GrammarSequenceBuilder> buildSequence) => AddSequence(this, buildSequence);

    public GrammarSetBuilder Optional(Action<GrammarOptionalBuilder> buildOptional) => AddOptional(this, buildOptional);

    public GrammarSetBuilder Alternative(Action<GrammarAlternativeBuilder> buildAlternative) => AddAlternative(this, buildAlternative);

    public GrammarSetBuilder Set(Action<GrammarSetBuilder> buildSet) => AddSet(this, buildSet);

    public GrammarSetBuilder Repeat(IParser parser, int minimumCount = 0) => AddParser(this, new Repeat(parser, minimumCount));

    public GrammarSetBuilder Repeat<TParser>() where TParser : class, IParser<TParser> => AddParser(this, Parsers.Repeat<TParser>.Instance);

    public GrammarSetBuilder Parser(IParser parser) => AddParser(this, parser);

    public GrammarSetBuilder Parser<TParser>() where TParser : class, IParser<TParser> => AddParser(this, TParser.Instance);

    public override IParser Build() => new Set([.. GetChildrenNonEmpty()]);
}
