using System.Globalization;
using Ufw.Mock.Cli;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Services;

internal sealed class UfwRuleService(
    UfwGlobalOptions options,
    UfwRuleMutationCoordinator mutations,
    UfwRuleMutationReporter reporter,
    UfwConfirmationService confirmation,
    UfwCommandExecutionService execution)
{
    public int Add(FirewallAction action, IReadOnlyList<string> arguments, bool routed) => execution.Execute(() =>
    {
        if (options.Force)
        {
            throw new UfwCliException("Invalid syntax");
        }
        reporter.WriteResults(mutations.Add(action, arguments, routed));
        return 0;
    });

    public int Insert(IReadOnlyList<string> arguments, bool routed) => execution.Execute(() =>
    {
        if (arguments.Count < 2 || !int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int number))
        {
            throw new UfwCliException("Insert requires a one-based rule number followed by a rule.");
        }

        FirewallAction action = ParseAction(arguments[1]);
        reporter.WriteResults(mutations.Insert(action, arguments.Skip(2).ToArray(), routed, number));
        return 0;
    });

    public int Prepend(IReadOnlyList<string> arguments, bool routed) => execution.Execute(() =>
    {
        if (arguments.Count < 1)
        {
            throw new UfwCliException("Prepend requires a rule.");
        }

        FirewallAction action = ParseAction(arguments[0]);
        reporter.WriteResults(mutations.Prepend(action, arguments.Skip(1).ToArray(), routed));
        return 0;
    });

    public int Delete(IReadOnlyList<string> arguments, bool routed) => execution.Execute(() =>
    {
        if (arguments.Count == 0)
        {
            throw new UfwCliException("Delete requires a rule or rule number.");
        }

        if (arguments.Count == 1 && int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int displayNumber))
        {
            if (routed)
            {
                throw new UfwCliException("'route delete NUM' unsupported. Use 'delete NUM' instead.");
            }
            return DeleteByNumber(displayNumber);
        }

        FirewallAction action = ParseAction(arguments[0]);
        reporter.WriteDeleted(mutations.Delete(action, arguments.Skip(1).ToArray(), routed));
        return 0;
    });

    private int DeleteByNumber(int displayNumber)
    {
        if (displayNumber <= 0)
        {
            throw new UfwCliException("Rule numbers are one-based.");
        }
        if (!options.Force && !confirmation.Confirm($"Deleting rule {displayNumber}. Proceed with operation (y|n)? "))
        {
#pragma warning disable CA1303 // Fixed English text is part of the UFW-compatible CLI surface.
            Console.WriteLine("Aborted");
#pragma warning restore CA1303
            return 0;
        }

        reporter.WriteDeleted(mutations.DeleteByNumber(displayNumber));
        return 0;
    }

    private static FirewallAction ParseAction(string value) => value.ToUpperInvariant() switch
    {
        "ALLOW" => FirewallAction.Allow,
        "DENY" => FirewallAction.Deny,
        "REJECT" => FirewallAction.Reject,
        "LIMIT" => FirewallAction.Limit,
        _ => throw new UfwCliException($"Unknown rule action '{value}'."),
    };
}
