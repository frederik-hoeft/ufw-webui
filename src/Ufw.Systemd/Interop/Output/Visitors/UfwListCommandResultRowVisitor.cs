using Ufw.Systemd.Interop.Output.Grammars;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.SyntaxNodes;

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

    public void Visit(Ipv4CidrSyntaxNode syntaxNode)
    {
        if (syntaxNode.HasParent(UfwListCommandResultGrammar.SourceGroup))
        {
            result.Source = syntaxNode.Evaluate();
        }
        else if (syntaxNode.HasParent(UfwListCommandResultGrammar.DestinationGroup))
        {
            result.Destination = syntaxNode.Evaluate();
        }
        else
        {
            throw new InvalidOperationException("IPv4 node has unknown parent.");
        }
    }

    public void Visit(ActionSyntaxNode syntaxNode)
    {
        FirewallAction action = syntaxNode.Evaluate();
        result.Type = action.RuleType;
        result.Direction = action.Direction;
    }

    public void Visit(OutSyntaxNode syntaxNode)
    {
        // at this time, a destination interface must have been set
        if (string.IsNullOrEmpty(result.DestinationInterface))
        {
            throw new InvalidOperationException("Out node found but destination interface is not set.");
        }
    }

    public void Visit(JsonCommentSyntaxNode syntaxNode) => result.Context = syntaxNode.Evaluate();

    public void Visit(CommentSyntaxNode syntaxNode) => result.Comment = syntaxNode.Evaluate();

    public void Visit(AnywhereSyntaxNode anywhereSyntaxNode) { /* No action needed, anywhere is implicit */ }
}
