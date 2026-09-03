using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Systemd.Interop.Output.Grammars;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.SyntaxNodes;
using ParsedFirewallAction = Ufw.Systemd.Interop.Output.Model.FirewallAction;

namespace Ufw.Systemd.Interop.Output.Visitors;

internal sealed class UfwListCommandResultRowVisitor(UfwListCommandResultRow result) : INodeVisitor
{
    public void Visit(RowNumberSyntaxNode rowNumber) => result.RowNumber = rowNumber.Evaluate();

    public void Visit(NetworkInterfaceSyntaxNode syntaxNode)
    {
        if (syntaxNode.HasParent(UfwListCommandResultGrammar.SourceGroup))
        {
            result.SourceInterface = syntaxNode.Evaluate();
        }
        else if (syntaxNode.HasParent(UfwListCommandResultGrammar.DestinationGroup))
        {
            result.DestinationInterface = syntaxNode.Evaluate();
        }
        else
        {
            throw new InvalidOperationException("Interface node has unknown parent.");
        }
    }

    public void Visit(PortSyntaxNode syntaxNode)
    {
        if (syntaxNode.HasParent(UfwListCommandResultGrammar.SourceGroup))
        {
            result.SourcePorts = syntaxNode.Evaluate();
        }
        else if (syntaxNode.HasParent(UfwListCommandResultGrammar.DestinationGroup))
        {
            result.DestinationPorts = syntaxNode.Evaluate();
        }
        else
        {
            throw new InvalidOperationException("Port node has unknown parent.");
        }
    }

    public void Visit(ProtocolSyntaxNode syntaxNode) => result.Protocol = syntaxNode.Evaluate();

    public void Visit(Ipv4CidrSyntaxNode syntaxNode) => AssignAddress(syntaxNode, syntaxNode.Evaluate());

    public void Visit(Ipv6CidrSyntaxNode syntaxNode)
    {
        result.AddressFamily = FirewallAddressFamily.IPv6;
        AssignAddress(syntaxNode, syntaxNode.Evaluate());
    }

    public void Visit(V6HintSyntaxNode syntaxNode) => result.AddressFamily = FirewallAddressFamily.IPv6;

    public void Visit(ActionSyntaxNode syntaxNode)
    {
        ParsedFirewallAction action = syntaxNode.Evaluate();
        result.Type = action.RuleType;
        result.Direction = action.Direction;
    }

    public void Visit(OutSyntaxNode syntaxNode)
    {
        if (string.IsNullOrEmpty(result.DestinationInterface))
        {
            throw new InvalidOperationException("Out node found but destination interface is not set.");
        }
    }

    public void Visit(JsonCommentSyntaxNode syntaxNode) => result.Context = syntaxNode.Evaluate();

    public void Visit(CommentSyntaxNode syntaxNode) => result.Comment = syntaxNode.Evaluate();

    public void Visit(AnywhereSyntaxNode anywhereSyntaxNode) { }

    private void AssignAddress(ISyntaxNode syntaxNode, string address)
    {
        if (syntaxNode.HasParent(UfwListCommandResultGrammar.SourceGroup))
        {
            result.Source = address;
        }
        else if (syntaxNode.HasParent(UfwListCommandResultGrammar.DestinationGroup))
        {
            result.Destination = address;
        }
        else
        {
            throw new InvalidOperationException("Address node has unknown parent.");
        }
    }
}
