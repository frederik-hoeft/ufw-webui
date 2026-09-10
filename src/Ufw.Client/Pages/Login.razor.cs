using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using System.ComponentModel.DataAnnotations;
using System.Net;
using Ufw.Client.Api;

namespace Ufw.Client.Pages;

public sealed partial class Login
{
    private readonly CancellationTokenSource _lifetime = new();
    private MudForm? _form;
    private bool _isValid;
    private bool _busy;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string? _error;

    private Func<string?, string?> EmailValidation => value =>
        new EmailAddressAttribute().IsValid(value) ? null : AuthenticationText["EmailInvalid"].Value;

    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    protected async override Task OnInitializedAsync()
    {
        AuthenticationState state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated == true)
        {
            NavigateAfterLogin();
        }
    }

    public void Dispose()
    {
        _password = string.Empty;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task LoginAsync()
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

            await AuthenticationService.LoginAsync(_email, _password, _lifetime.Token);
            _password = string.Empty;
            NavigateAfterLogin();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            _error = AuthenticationText["InvalidCredentials"];
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

    private void NavigateAfterLogin()
    {
        string target = IsLocalReturnUrl(ReturnUrl) ? ReturnUrl! : "/rules";
        Navigation.NavigateTo(target, replace: true);
    }

    private static bool IsLocalReturnUrl(string? returnUrl)
    {
        return returnUrl is { Length: > 0 }
            && returnUrl[0] == '/'
            && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'));
    }
}
