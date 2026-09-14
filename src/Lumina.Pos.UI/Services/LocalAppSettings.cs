using System.Text.Json;

namespace Lumina.Pos.UI.Services;

public sealed class LocalAppSettings
{
    public string StoreName { get; set; } = "Tienda local";
    public string CashierDefaultName { get; set; } = "Cajero";
    public string ConnectionString { get; set; } = "Data Source=lumina.dev.db";
    public string? TenantId { get; set; }
    public bool FiscalEnabled { get; set; } = false;
    public string Theme { get; set; } = "DarkPos";
    public string CurrencySymbol { get; set; } = "€";
    public decimal DefaultVatRate { get; set; } = 0.21m;
    public List<LocalProductDto> Products { get; set; } = new();
}

public sealed class LocalProductDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public decimal VatRate { get; set; } = 0.21m;
    public string? Barcode { get; set; }
    public string? Family { get; set; }
}

public static class LocalSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    public static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LuminaPos", "local-settings.json");

    public static LocalAppSettings Load()
    {
        try
        {
            var path = FilePath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<LocalAppSettings>(json, JsonOpts) ?? CreateDefault();
            }
        }
        catch { }
        return CreateDefault();
    }

    public static void Save(LocalAppSettings settings)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOpts));
    }

    public static LocalAppSettings CreateDefault()
    {
        var s = new LocalAppSettings
        {
            FiscalEnabled = false,
            Products =
            {
                new() { Code = "DEP-FRUTA", Name = "Fruta surtida", Price = 1.99m, Barcode = "FRUTA", Family = "Sección" },
                new() { Code = "DEP-PAN", Name = "Pan barra", Price = 1.20m, Barcode = "PAN", Family = "Sección" },
                new() { Code = "DEP-CERVEZA", Name = "Cerveza 33cl", Price = 1.50m, Barcode = "CERVEZA", Family = "Sección" },
                new() { Code = "DEP-VARIOS", Name = "Varios", Price = 0.50m, Barcode = "VARIOS", Family = "Sección" },
            }
        };
        Save(s);
        return s;
    }
}
