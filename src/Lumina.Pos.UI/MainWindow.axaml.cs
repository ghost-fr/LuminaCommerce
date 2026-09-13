using Avalonia.Controls;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Catalogue;
using Lumina.Contracts.Devices;
using Lumina.Contracts.Pos;
using Lumina.Contracts.Stock;
using Lumina.Pos.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.Pos.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var auth = App.Services.GetRequiredService<IAuthService>();
        var pos = App.Services.GetRequiredService<IPosSaleService>();
        var catalogue = App.Services.GetRequiredService<IProductCatalogueService>();
        var stock = App.Services.GetRequiredService<IStockService>();
        var printer = App.Services.GetRequiredService<IReceiptPrinter>();
        var scale = App.Services.GetRequiredService<IScaleService>();
        DataContext = new ShellViewModel(auth, pos, catalogue, stock, printer, scale);
    }
}
