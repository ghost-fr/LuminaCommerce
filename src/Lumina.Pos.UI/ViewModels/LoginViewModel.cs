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
                    ErrorMessage = result.FailureReason ?? "Usuario o contraseña incorrectos.";
                    break;

                case LoginStatus.UserInactive:
                    ErrorMessage = result.FailureReason ?? "Esta cuenta está inactiva. Contacte con un administrador.";
                    break;

                case LoginStatus.StoreNotAssigned:
                    ErrorMessage = result.FailureReason ?? "Esta cuenta no tiene tienda asignada.";
                    break;

                default:
                    ErrorMessage = "No se ha podido entrar. Inténtelo de nuevo.";
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error inesperado: {ex.Message}";
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
