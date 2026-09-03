using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Parsers;

internal sealed class JsonComment(string? name = null) : IParser<JsonComment>
{
    public static JsonComment Instance { get; } = new();

    public string? Name => name;

    public IParser NamedCopy(string name) => new JsonComment(name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        try
        {
            UfwRuleContext? context = JsonSerializer.Deserialize(
                input.AsSpan(offset),
                global::Ufw.Systemd.Interop.Output.UfwJsonSerializerContext.Default.UfwRuleContext);
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
