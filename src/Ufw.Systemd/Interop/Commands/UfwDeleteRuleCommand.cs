using System.Collections.Immutable;
using System.Globalization;

namespace Ufw.Systemd.Interop.Commands;

internal sealed class UfwDeleteRuleCommand(int displayNumber) : IUfwCommand
{
    public ImmutableArray<string> BuildArguments()
    {
        if (displayNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayNumber), displayNumber, "UFW rule numbers are 1-based.");
        }

        return ["--force", "delete", displayNumber.ToString(CultureInfo.InvariantCulture)];
    }

    public void SetOutput(string output)
    {
    }
}
