using Ufw.Shared.Parsing.Visitors;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

namespace Ufw.Systemd.Interop.Output.Visitors;

/// <summary>
/// Interprets a parsed UFW numbered-rule syntax tree into the daemon's row model while carrying typed endpoint context through endpoint subtrees.
/// </summary>
internal interface IUfwListCommandResultRowVisitor : INodeVisitor
{
    /// <summary>Enters a source or destination endpoint subtree.</summary>
    void EnterEndpoint(EndpointRole role);

    /// <summary>Leaves the current source or destination endpoint subtree.</summary>
    void ExitEndpoint(EndpointRole role);

    /// <summary>Visits the numbered row prefix.</summary>
    void Visit(RowNumberSyntaxNode syntaxNode);

    /// <summary>Visits a parsed network-interface segment.</summary>
    void Visit(NetworkInterfaceSyntaxNode syntaxNode);

    /// <summary>Visits a parsed port segment.</summary>
    void Visit(PortSyntaxNode syntaxNode);

    /// <summary>Visits a parsed protocol segment.</summary>
    void Visit(ProtocolSyntaxNode syntaxNode);

    /// <summary>Visits a parsed IPv4 endpoint address.</summary>
    void Visit(Ipv4CidrSyntaxNode syntaxNode);

    /// <summary>Visits a parsed IPv6 endpoint address.</summary>
    void Visit(Ipv6CidrSyntaxNode syntaxNode);

    /// <summary>Visits an IPv6 marker emitted by UFW.</summary>
    void Visit(V6HintSyntaxNode syntaxNode);

    /// <summary>Visits the rule action and direction segment.</summary>
    void Visit(ActionSyntaxNode syntaxNode);

    /// <summary>Visits UFW's outbound-interface marker.</summary>
    void Visit(OutSyntaxNode syntaxNode);

    /// <summary>Visits a rule comment.</summary>
    void Visit(CommentSyntaxNode syntaxNode);

    /// <summary>Visits an unconstrained endpoint marker.</summary>
    void Visit(AnywhereSyntaxNode anywhereSyntaxNode);
}
