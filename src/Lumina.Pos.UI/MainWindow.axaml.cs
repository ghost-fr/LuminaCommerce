using Avalonia.Controls;
using Lumina.Contracts.Auth;
using Lumina.Pos.UI.ViewModels;
using Lumina.Pos.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.Pos.UI;

/// <summary>
/// Root window. Shows login when <see cref="IAuthService.CurrentSession"/> is null,
/// otherwise the capability-gated shell. No separate isLoggedIn flag.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IAuthService _auth;

    public MainWindow()
    {
        InitializeComponent();
        _auth = App.Services.GetRequiredService<IAuthService>();
        ShowLoginOrShell();
    }

    private void ShowLoginOrShell()
    {
        if (_auth.CurrentSession is null)
        {
            var vm = new LoginViewModel(_auth, onLoginSucceeded: ShowLoginOrShell);
            Host.Content = new LoginView { DataContext = vm };
        }
        else
        {
            var vm = new ShellViewModel(_auth, onLoggedOut: ShowLoginOrShell);
            Host.Content = new ShellView { DataContext = vm };
        }
    }
}
