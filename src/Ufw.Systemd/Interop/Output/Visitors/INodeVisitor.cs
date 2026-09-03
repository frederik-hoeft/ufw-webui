using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Visitors;

internal interface INodeVisitor
{
    void Visit(RowNumberSyntaxNode rowNumber);
    void Visit(NetworkInterfaceSyntaxNode syntaxNode);
    void Visit(PortSyntaxNode syntaxNode);
    void Visit(ProtocolSyntaxNode syntaxNode);
    void Visit(Ipv4CidrSyntaxNode syntaxNode);
    void Visit(Ipv6CidrSyntaxNode syntaxNode);
    void Visit(V6HintSyntaxNode syntaxNode);
    void Visit(ActionSyntaxNode syntaxNode);
    void Visit(OutSyntaxNode syntaxNode);
    void Visit(JsonCommentSyntaxNode syntaxNode);
    void Visit(CommentSyntaxNode syntaxNode);
    void Visit(AnywhereSyntaxNode anywhereSyntaxNode);
}
