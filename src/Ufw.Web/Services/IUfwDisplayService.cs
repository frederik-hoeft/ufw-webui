namespace Ufw.Web.Services;

internal interface IUfwDisplayService
{
    string GetDisplayValue(string? value);
    bool IsAnyValue(string? value);
}
