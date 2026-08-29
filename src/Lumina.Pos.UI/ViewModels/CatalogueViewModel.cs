using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Catalogue;

namespace Lumina.Pos.UI.ViewModels;

public partial class CatalogueRowViewModel : ObservableObject
{
    public Guid ProductId { get; }
    public string Barcode { get; }
    public string Name { get; }
    public decimal CurrentPrice { get; }
    public decimal? OriginalPrice { get; }
    public decimal VatRate { get; }
    public bool HasActivePromotion { get; }

    public string PriceDisplay => HasActivePromotion && OriginalPrice is { } orig
        ? $"{CurrentPrice:0.00} (was {orig:0.00})"
        : CurrentPrice.ToString("0.00");

    public CatalogueRowViewModel(IProductSummary p)
    {
        ProductId = p.ProductId;
        Barcode = p.Barcode;
        Name = p.Name;
        CurrentPrice = p.CurrentPrice;
        OriginalPrice = p.OriginalPrice;
        VatRate = p.VatRate;
        HasActivePromotion = p.HasActivePromotion;
    }
}

public partial class CatalogueViewModel : ObservableObject
{
    private readonly IProductCatalogueService _catalogue;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _totalCount;

    public ObservableCollection<CatalogueRowViewModel> Products { get; } = new();

    public CatalogueViewModel(IProductCatalogueService catalogue)
    {
        _catalogue = catalogue;
        _ = SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _catalogue.SearchAsync(
                new ProductSearchRequest(
                    string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                    Page: 1,
                    PageSize: 100));

            Products.Clear();
            foreach (var item in result.Items)
                Products.Add(new CatalogueRowViewModel(item));

            TotalCount = result.TotalCount;
            StatusMessage = TotalCount == 0
                ? "No products found. Seed catalogue data or refine search."
                : $"{TotalCount} product(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search failed: {ex.Message}";
            Products.Clear();
            TotalCount = 0;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
