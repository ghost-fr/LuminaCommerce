using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// ViewModel for the POS login screen. Resolves <see cref="IAuthService"/> from
/// the composition root (App.Services). Never constructs AuthService directly.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly Action _onLoginSucceeded;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(IAuthService auth, Action onLoginSucceeded)
    {
        _auth = auth;
        _onLoginSucceeded = onLoginSucceeded;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _auth.LoginAsync(new LoginRequest(Username.Trim(), Password));

            switch (result.Status)
            {
                case LoginStatus.Success:
                    Password = string.Empty; // clear from memory as soon as possible
                    _onLoginSucceeded();
                    break;

                case LoginStatus.InvalidCredentials:
                    ErrorMessage = result.FailureReason ?? "Invalid username or password.";
                    break;

                case LoginStatus.UserInactive:
                    ErrorMessage = result.FailureReason ?? "This account is inactive. Contact an administrator.";
                    break;

                case LoginStatus.StoreNotAssigned:
                    ErrorMessage = result.FailureReason ?? "No store is assigned to this account.";
                    break;

                default:
                    ErrorMessage = "Login failed. Please try again.";
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            LoginCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanLogin() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);

    partial void OnUsernameChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnPasswordChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();
}
