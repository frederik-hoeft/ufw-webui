namespace Ufw.Mock.Services;

internal sealed class UfwConfirmationService
{
    public bool Confirm(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        Console.Write(prompt);
        string? answer = Console.ReadLine();
        return answer?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
    }
}
