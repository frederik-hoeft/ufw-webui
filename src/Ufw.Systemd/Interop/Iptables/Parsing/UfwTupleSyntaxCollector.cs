using Ufw.Shared.Firewall;
using Ufw.Systemd.Interop.Iptables.Parsing.SyntaxNodes;

namespace Ufw.Systemd.Interop.Iptables.Parsing;

/// <summary>Collects typed tuple nodes without reinterpreting field positions or token syntax.</summary>
internal sealed class UfwTupleSyntaxCollector : IUfwTupleVisitor
{
    private FirewallAction? _action;
    private bool _forward;
    private UfwTupleLogging _logging;
    private string? _protocol;
    private string? _source;
    private string? _destination;
    private string? _sourcePorts;
    private string? _destinationPorts;
    private string _direction = "in";
    private string _interfaceIn = string.Empty;
    private string _interfaceOut = string.Empty;
    private string? _sourceApplication;
    private string? _destinationApplication;
    private string? _comment;

    public void Visit(UfwTupleActionSyntaxNode node)
    {
        _action = node.Action;
        _forward = node.Forward;
        _logging = node.Logging;
    }

    public void Visit(UfwTupleProtocolSyntaxNode node) => _protocol = node.Value;

    public void Visit(UfwTuplePortSyntaxNode node)
    {
        if (node.Role == UfwTupleFieldRole.Source)
        {
            _sourcePorts = node.Value;
        }
        else
        {
            _destinationPorts = node.Value;
        }
    }

    public void Visit(UfwTupleAddressSyntaxNode node)
    {
        if (node.Role == UfwTupleFieldRole.Source)
        {
            _source = node.Value;
        }
        else
        {
            _destination = node.Value;
        }
    }

    public void Visit(UfwTupleApplicationSyntaxNode node)
    {
        if (node.Role == UfwTupleFieldRole.Source)
        {
            _sourceApplication = node.Value;
        }
        else
        {
            _destinationApplication = node.Value;
        }
    }

    public void Visit(UfwTupleDirectionSyntaxNode node)
    {
        _direction = node.Direction;
        _interfaceIn = node.InterfaceIn;
        _interfaceOut = node.InterfaceOut;
    }

    public void Visit(UfwTupleCommentSyntaxNode node) => _comment = node.Value;

    public UfwTupleSyntax Build() => new()
    {
        Action = _action!.Value,
        Forward = _forward,
        Logging = _logging,
        Protocol = _protocol!,
        Source = _source!,
        Destination = _destination!,
        SourcePorts = _sourcePorts!,
        DestinationPorts = _destinationPorts!,
        Direction = _direction,
        InterfaceIn = _interfaceIn,
        InterfaceOut = _interfaceOut,
        SourceApplication = _sourceApplication,
        DestinationApplication = _destinationApplication,
        Comment = _comment,
    };
}
