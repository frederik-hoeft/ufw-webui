using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Shared.Firewall.Iptables.Parsing.SyntaxNodes;

internal sealed class IgnoredIptablesLineSyntaxNode(string? name) : SyntaxNodeBase(name);
