using System.Text;
using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Shared.Parsing.SyntaxNodes;

public interface ISyntaxNode
{
    string? Name { get; }

    ISyntaxNode? Parent { get; set; }

    bool HasParent(string name);

    void Accept(INodeVisitor visitor);

    string ToString();

    void ToString(StringBuilder builder, int indentLevel);
}
