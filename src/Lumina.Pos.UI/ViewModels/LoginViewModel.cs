using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Login screen VM. Bound strictly to <see cref="IAuthService"/> per CONTRACTS.md §0.
/// Source of truth for "logged in" is <see cref="IAuthService.CurrentSession"/> —
/// this VM never tracks a separate isLoggedIn flag.
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
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            // Single-store tenants: omit RequestedStoreId — backend picks the default.
            var result = await _auth.LoginAsync(new LoginRequest(Username.Trim(), Password));

            if (result.Status == LoginStatus.Success && result.Session is not null)
            {
                Password = string.Empty;
                _onLoginSucceeded();
                return;
            }

            ErrorMessage = result.Status switch
            {
                LoginStatus.InvalidCredentials => "Invalid username or password.",
                LoginStatus.UserInactive => "This account is inactive. Contact an administrator.",
                LoginStatus.StoreNotAssigned => "No store is assigned to this account. Contact an administrator.",
                _ => result.FailureReason ?? "Login failed."
            };
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin() =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password);

    partial void OnUsernameChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnPasswordChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();
}
