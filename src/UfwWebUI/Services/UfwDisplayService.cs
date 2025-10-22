namespace UfwWebUI.Services;

public interface IUfwDisplayService
{
    string GetDisplayValue(string? value);
    bool IsAnyValue(string? value);
}

public sealed class UfwDisplayService : IUfwDisplayService
{
    public string GetDisplayValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "any", StringComparison.OrdinalIgnoreCase))
        {
            return "any";
        }

        return value;
    }

    public bool IsAnyValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, "any", StringComparison.OrdinalIgnoreCase);
    }
}
