using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Responses;

namespace Ufw.Systemd.Firewall;

internal static partial class RuleSpecificationValidator
{
    public const int MAX_COMMENT_LENGTH = 200;
    public const int MAX_INTERFACE_LENGTH = 32;

    public static bool TryValidate(
        FirewallRuleSpecification specification,
        [NotNullWhen(false)] out ModelValidationErrorResponse? error)
    {
        ArgumentNullException.ThrowIfNull(specification);
        List<ModelValidationError> errors = [];

        if (!Enum.IsDefined(specification.Action))
        {
            errors.Add(new ModelValidationError(nameof(specification.Action), "Action is not supported."));
        }

        if (!Enum.IsDefined(specification.Direction))
        {
            errors.Add(new ModelValidationError(nameof(specification.Direction), "Direction is not supported."));
        }

        if (!Enum.IsDefined(specification.Protocol))
        {
            errors.Add(new ModelValidationError(nameof(specification.Protocol), "Protocol is not supported."));
        }

        ValidateAddress(nameof(specification.Source), specification.Source, errors);
        ValidateAddress(nameof(specification.Destination), specification.Destination, errors);
        ValidatePorts(nameof(specification.SourcePorts), specification.SourcePorts, errors);
        ValidatePorts(nameof(specification.DestinationPorts), specification.DestinationPorts, errors);
        ValidateInterface(nameof(specification.SourceInterface), specification.SourceInterface, errors);
        ValidateInterface(nameof(specification.DestinationInterface), specification.DestinationInterface, errors);
        ValidateComment(specification.Comment, errors);

        if (errors.Count > 0)
        {
            error = new ModelValidationErrorResponse([.. errors]);
            return false;
        }

        error = null;
        return true;
    }

    private static void ValidateAddress(string propertyName, string? address, List<ModelValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(address)
            || address.Equals(RuleSpecificationNormalizer.ANY, StringComparison.OrdinalIgnoreCase)
            || address.Equals("Anywhere", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string trimmed = address.Trim();
        int slash = trimmed.IndexOf('/');
        string host = slash < 0 ? trimmed : trimmed[..slash];
        if (!IPAddress.TryParse(host, out IPAddress? parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            errors.Add(new ModelValidationError(propertyName, "Address must be IPv4, IPv4 CIDR, or 'any'."));
            return;
        }

        if (slash < 0)
        {
            return;
        }

        if (!int.TryParse(trimmed[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int prefix)
            || prefix is < 0 or > 32)
        {
            errors.Add(new ModelValidationError(propertyName, "IPv4 prefix length must be between 0 and 32."));
        }
    }

    private static void ValidatePorts(string propertyName, string? ports, List<ModelValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(ports))
        {
            return;
        }

        string trimmed = ports.Trim();
        if (!PortsRegex().IsMatch(trimmed))
        {
            errors.Add(new ModelValidationError(propertyName, "Ports must be a comma-separated list of ports or port:port ranges."));
            return;
        }

        foreach (string part in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int colon = part.IndexOf(':');
            if (colon < 0)
            {
                if (!IsPort(part))
                {
                    errors.Add(new ModelValidationError(propertyName, "Port numbers must be between 1 and 65535."));
                    return;
                }

                continue;
            }

            string start = part[..colon];
            string end = part[(colon + 1)..];
            if (!IsPort(start) || !IsPort(end))
            {
                errors.Add(new ModelValidationError(propertyName, "Port ranges must use values between 1 and 65535."));
                return;
            }

            int startPort = int.Parse(start, CultureInfo.InvariantCulture);
            int endPort = int.Parse(end, CultureInfo.InvariantCulture);
            if (startPort > endPort)
            {
                errors.Add(new ModelValidationError(propertyName, "Port range start must be less than or equal to the end."));
                return;
            }
        }
    }

    private static void ValidateInterface(string propertyName, string? networkInterface, List<ModelValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(networkInterface))
        {
            return;
        }

        string trimmed = networkInterface.Trim();
        if (trimmed.Length > MAX_INTERFACE_LENGTH || !InterfaceRegex().IsMatch(trimmed))
        {
            errors.Add(new ModelValidationError(propertyName, "Interface name contains unsupported characters."));
        }
    }

    private static void ValidateComment(string? comment, List<ModelValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        string trimmed = comment.Trim();
        if (trimmed.Length > MAX_COMMENT_LENGTH || !CommentRegex().IsMatch(trimmed))
        {
            errors.Add(new ModelValidationError(
                nameof(FirewallRuleSpecification.Comment),
                "Comment must be 1-200 characters of a restricted safe alphabet."));
        }
    }

    private static bool IsPort(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int port) && port is >= 1 and <= 65535;

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex InterfaceRegex();

    [GeneratedRegex(@"^[1-9][0-9]{0,4}(:[1-9][0-9]{0,4})?(,[1-9][0-9]{0,4}(:[1-9][0-9]{0,4})?)*$", RegexOptions.CultureInvariant)]
    private static partial Regex PortsRegex();

    [GeneratedRegex(@"^[A-Za-z0-9 ._@:+/=-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CommentRegex();
}
