using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.SyntaxNodes;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class JsonComment(string? name = null) : ParserBase<IUfwListCommandResultRowVisitor>, IParser<JsonComment>
{
    public static JsonComment Instance { get; } = new();

    public override string? Name => name;

    public override IParser NamedCopy(string name) => new JsonComment(name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        try
        {
            UfwRuleContext? context = JsonSerializer.Deserialize(input.AsSpan(offset), UfwJsonSerializerContext.Default.UfwRuleContext);
            if (context is not null)
            {
                charsConsumed = input.Length - offset;
                syntaxNode = new JsonCommentSyntaxNode(Name, context);
                return true;
            }
        }
        catch (JsonException) { }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}
