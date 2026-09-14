using Ufw.Shared.Parsing.Parsers;

namespace Ufw.Shared.Parsing.Grammars.Builders;

public abstract class GrammarBuilder
{
    public abstract IParser Build();

    protected static IParser CreateSequence(Action<GrammarSequenceBuilder> buildSequence)
    {
        GrammarSequenceBuilder sequenceBuilder = new();
        buildSequence(sequenceBuilder);
        return sequenceBuilder.Build();
    }

    protected static IParser CreateSequence(params ReadOnlySpan<IParser> parsers) => new Sequence([.. parsers]);

    protected static IParser CreateAlternative(Action<GrammarAlternativeBuilder> buildAlternative)
    {
        GrammarAlternativeBuilder alternativeBuilder = new();
        buildAlternative(alternativeBuilder);
        return alternativeBuilder.Build();
    }

    protected static IParser CreateAlternative(params ReadOnlySpan<IParser> parsers) => new Alternative([.. parsers]);

    protected static IParser CreateOptional(Action<GrammarOptionalBuilder> buildOptional)
    {
        GrammarOptionalBuilder optionalBuilder = new();
        buildOptional(optionalBuilder);
        return optionalBuilder.Build();
    }

    protected static IParser CreateOptional(IParser parser) => new Optional(parser);
}
