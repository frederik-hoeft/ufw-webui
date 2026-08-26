using Ufw.Systemd.Interop.Output.Parsers;

namespace Ufw.Systemd.Interop.Output.Grammars.Builders;

internal abstract class GrammarCollectionBuilder : GrammarBuilder
{
    protected List<IParser> Children { get; } = [];

    protected T AddSequence<T>(T instance, Action<GrammarSequenceBuilder> buildSequence)
    {
        Children.Add(CreateSequence(buildSequence));
        return instance;
    }

    protected T AddOptional<T>(T instance, Action<GrammarOptionalBuilder> buildOptional)
    {
        Children.Add(CreateOptional(buildOptional));
        return instance;
    }

    protected T AddAlternative<T>(T instance, Action<GrammarAlternativeBuilder> buildAlternative)
    {
         Children.Add(CreateAlternative(buildAlternative));
        return instance;
    }

    protected T AddParser<T>(T instance, IParser parser)
    {
        Children.Add(parser);
        return instance;
    }

    protected List<IParser> GetChildrenNonEmpty()
    {
        if (Children.Count == 0)
        {
            throw new InvalidOperationException("No children have been added to the grammar collection.");
        }
        return Children;
    }
}
