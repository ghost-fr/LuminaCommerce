using Avalonia.Controls;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Pos;
using Lumina.Pos.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.Pos.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Resolve from composition root only — never construct application services here.
        var auth = App.Services.GetRequiredService<IAuthService>();
        var pos = App.Services.GetRequiredService<IPosSaleService>();
        DataContext = new ShellViewModel(auth, pos);
    }
}
