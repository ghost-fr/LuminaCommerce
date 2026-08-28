using Avalonia.Controls;
using Lumina.Contracts.Auth;
using Lumina.Pos.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.Pos.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Resolve IAuthService from the composition root (App.Services).
        // Never construct AuthService or other application services here.
        var auth = App.Services.GetRequiredService<IAuthService>();
        DataContext = new ShellViewModel(auth);
    }
}
