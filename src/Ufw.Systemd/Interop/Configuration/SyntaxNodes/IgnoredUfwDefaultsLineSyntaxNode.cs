using Ufw.Shared.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Configuration.SyntaxNodes;

internal sealed class IgnoredUfwDefaultsLineSyntaxNode(string? name) : SyntaxNodeBase(name)
{
}
