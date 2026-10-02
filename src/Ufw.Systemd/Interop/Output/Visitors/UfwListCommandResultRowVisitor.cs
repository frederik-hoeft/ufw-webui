using Ufw.Shared.Firewall;
using Ufw.Systemd.Interop.Output.Model;
using Ufw.Systemd.Interop.Output.SyntaxNodes;
using ParsedFirewallAction = Ufw.Systemd.Interop.Output.Model.FirewallAction;

namespace Ufw.Systemd.Interop.Output.Visitors;

internal sealed class UfwListCommandResultRowVisitor(UfwListCommandResultRow result) : IUfwListCommandResultRowVisitor
{
    private EndpointRole? _endpointRole;

    public void EnterEndpoint(EndpointRole role)
    {
        if (_endpointRole is not null)
        {
            throw new InvalidOperationException($"Cannot enter endpoint '{role}' while endpoint '{_endpointRole}' is active.");
        }

        _endpointRole = role;
    }

    public void ExitEndpoint(EndpointRole role)
    {
        if (_endpointRole != role)
        {
            throw new InvalidOperationException($"Cannot exit endpoint '{role}' while endpoint '{_endpointRole?.ToString() ?? "<none>"}' is active.");
        }

        _endpointRole = null;
    }

    public void Visit(RowNumberSyntaxNode syntaxNode) => result.RowNumber = syntaxNode.Evaluate();

    public void Visit(NetworkInterfaceSyntaxNode syntaxNode)
    {
        if (CurrentEndpointRole == EndpointRole.Source)
        {
            result.SourceInterface = syntaxNode.Evaluate();
        }
        else
        {
            result.DestinationInterface = syntaxNode.Evaluate();
        }
    }

    public void Visit(PortSyntaxNode syntaxNode)
    {
        if (CurrentEndpointRole == EndpointRole.Source)
        {
            result.SourcePorts = syntaxNode.Evaluate();
        }
        else
        {
            result.DestinationPorts = syntaxNode.Evaluate();
        }
    }

    public void Visit(ProtocolSyntaxNode syntaxNode) => result.Protocol = syntaxNode.Evaluate();

    public void Visit(Ipv4CidrSyntaxNode syntaxNode) => AssignAddress(syntaxNode.Evaluate());

    public void Visit(Ipv6CidrSyntaxNode syntaxNode)
    {
        result.AddressFamily = FirewallAddressFamily.IPv6;
        AssignAddress(syntaxNode.Evaluate());
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

    public void Visit(CommentSyntaxNode syntaxNode) => result.Comment = syntaxNode.Evaluate();

    public void Visit(AnywhereSyntaxNode anywhereSyntaxNode) { }

    private EndpointRole CurrentEndpointRole => _endpointRole ?? throw new InvalidOperationException("Endpoint value found outside a source or destination endpoint context.");

    private void AssignAddress(string address)
    {
        if (CurrentEndpointRole == EndpointRole.Source)
        {
            result.Source = address;
        }
        else
        {
            result.Destination = address;
        }
    }
}
