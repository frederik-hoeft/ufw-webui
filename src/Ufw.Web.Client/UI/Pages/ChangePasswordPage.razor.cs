using MudBlazor;

namespace Ufw.Web.Client.UI.Pages;

public sealed partial class ChangePasswordPage
{
    private readonly CancellationTokenSource _lifetime = new();
    private MudForm? _form;
    private bool _isValid;
    private bool _busy;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _error;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(SettingsText["AccountBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(SettingsText["Title"], "/settings"),
        new BreadcrumbItem(SettingsText["ChangePasswordTitle"], null, disabled: true),
    ];

    private Func<string?, string?> ValidateConfirmation => value =>
        string.Equals(value, _newPassword, StringComparison.Ordinal) ? null : SettingsText["PasswordsDoNotMatch"].Value;

    public void Dispose()
    {
        ClearPasswords();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task ChangePasswordAsync()
    {
        if (_busy || _form is null)
        {
            return;
        }

        _busy = true;
        _error = null;
        try
        {
            await _form.ValidateAsync();
            if (!_form.IsValid)
            {
                return;
            }

            await AuthenticationService.ChangePasswordAsync(_currentPassword, _newPassword, _lifetime.Token);
            ClearPasswords();
            Snackbar.Add(SettingsText["PasswordChanged"], Severity.Success);
            Navigation.NavigateTo("/settings", replace: true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            _error = ClientErrors.Describe(exception).Message;
        }
        finally
        {
            _busy = false;
        }
    }

    private void ClearPasswords()
    {
        _currentPassword = string.Empty;
        _newPassword = string.Empty;
        _confirmPassword = string.Empty;
    }
}
