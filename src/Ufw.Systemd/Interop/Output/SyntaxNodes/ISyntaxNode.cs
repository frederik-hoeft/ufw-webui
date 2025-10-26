using System.Text;
using Ufw.Systemd.Interop.Output.Visitors;

namespace Ufw.Systemd.Interop.Output.SyntaxNodes;

internal interface ISyntaxNode
{
    string? Name { get; }

    ISyntaxNode? Parent { get; set; }

    bool HasParent(string name);

    void Accept(INodeVisitor visitor);

    string ToString();

    void ToString(StringBuilder builder, int indentLevel);
}
