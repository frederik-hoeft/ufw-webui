using Ufw.Shared.Parsing.Grammars.Builders;
using Ufw.Shared.Parsing.Parsers;

namespace Ufw.Shared.Parsing;

public static class Grammar
{
    public static IParser Sequence(Action<GrammarSequenceBuilder> buildSequence)
    {
        ArgumentNullException.ThrowIfNull(buildSequence);
        GrammarSequenceBuilder builder = new();
        buildSequence(builder);
        return builder.Build();
    }

    public static IParser Sequence(params ReadOnlySpan<IParser> parsers) => new Sequence([.. parsers]);

    public static IParser Alternative(Action<GrammarAlternativeBuilder> buildAlternative)
    {
        ArgumentNullException.ThrowIfNull(buildAlternative);
        GrammarAlternativeBuilder builder = new();
        buildAlternative(builder);
        return builder.Build();
    }

    public static IParser Alternative(params ReadOnlySpan<IParser> parsers) => new Alternative([.. parsers]);

    public static IParser Optional(Action<GrammarOptionalBuilder> buildOptional)
    {
        ArgumentNullException.ThrowIfNull(buildOptional);
        GrammarOptionalBuilder builder = new();
        buildOptional(builder);
        return builder.Build();
    }

    public static IParser Optional(IParser parser) => new Optional(parser);

    public static IParser Repeat(IParser parser, int minimumCount = 0) => new Repeat(parser, minimumCount);

    public static IParser Repeat<TParser>() where TParser : class, IParser<TParser> => Parsers.Repeat<TParser>.Instance;

    public static IParser Parser<TParser>() where TParser : class, IParser<TParser> => TParser.Instance;
}
