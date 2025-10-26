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
            // TODO: AOT compatibility / JSON context
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
            UfwRuleContext? context = JsonSerializer.Deserialize<UfwRuleContext>(input.AsSpan(offset));
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
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