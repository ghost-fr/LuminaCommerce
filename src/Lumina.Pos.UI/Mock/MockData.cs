namespace Lumina.Pos.UI.Mock;

/// <summary>
/// In-memory sample data for UI modules until backend read models land.
/// Not used by fiscal/sale commands — presentation only.
/// </summary>
public static class MockData
{
    public static readonly IReadOnlyList<ProductRow> Products =
    [
        new("001001", "Leche Entera Pascual 1L", "Lácteos", 1.15m, 0.21m, 120, true),
        new("001002", "Pan de Molde Bimbo 500g", "Panadería", 1.10m, 0.10m, 86, true),
        new("001003", "Café Natural Marcilla 250g", "Café", 2.45m, 0.21m, 45, true),
        new("001004", "Azúcar Blanco 1kg", "Básicos", 0.89m, 0.10m, 78, true),
        new("001005", "Aceite de Oliva 1L", "Aceites", 4.75m, 0.10m, 32, true),
        new("001006", "Detergente Ariel 40 Lavados", "Limpieza", 6.55m, 0.21m, 15, true),
        new("001007", "Agua Mineral 1.5L", "Bebidas", 0.55m, 0.10m, 200, true),
        new("001008", "Yogur Natural Pack 4", "Lácteos", 1.29m, 0.10m, 64, false),
    ];

    public static readonly IReadOnlyList<CustomerRow> Customers =
    [
        new("00001", "CLIENTE CONTADO", "—", "—", 0m),
        new("00002", "RESTAURANTE LA PLAZA", "B65432109", "933 22 11 00", 125.50m),
        new("00003", "HOTEL BARCELONA", "A88765432", "934 55 66 77", 35.20m),
        new("00004", "MARY JANE S.L.", "B11223344", "933 44 55 66", 0m),
        new("00005", "SUPERMAR S.L.", "B99887766", "934 66 77 88", 245.00m),
    ];

    public static readonly IReadOnlyList<TicketRow> Tickets =
    [
        new("00023", "13/08/2026", "13:20", 18.50m, "admin", "Aceptado"),
        new("00022", "13/08/2026", "12:45", 28.35m, "admin", "Aceptado"),
        new("00021", "13/08/2026", "11:32", 9.15m, "caja2", "Aceptado"),
        new("00020", "13/08/2026", "10:15", 7.94m, "admin", "Pendiente"),
        new("00019", "13/08/2026", "09:52", 18.60m, "admin", "Rechazado"),
    ];

    public static readonly IReadOnlyList<StockMovementRow> StockMovements =
    [
        new("13/08/2026 13:20", "Venta", "001001", -2, 118),
        new("13/08/2026 12:00", "Recepción", "001005", 24, 56),
        new("13/08/2026 10:15", "Ajuste", "001006", -1, 14),
        new("13/08/2026 09:00", "Transferencia", "001002", -12, 74),
    ];

    public static readonly IReadOnlyList<SupplierRow> Suppliers =
    [
        new("S001", "Distribuciones Norte SA", "A12345678", "info@norte.example", 12),
        new("S002", "Café & Té Mayoristas", "B87654321", "pedidos@cafe.example", 4),
        new("S003", "Limpieza Pro SL", "B55443322", "ventas@limpieza.example", 7),
    ];

    public static readonly IReadOnlyList<PurchaseOrderRow> PurchaseOrders =
    [
        new("PO-2026-0012", "Distribuciones Norte SA", "12/08/2026", "Recibido", 842.50m),
        new("PO-2026-0011", "Café & Té Mayoristas", "10/08/2026", "Pendiente", 210.00m),
        new("PO-2026-0010", "Limpieza Pro SL", "08/08/2026", "Parcial", 156.75m),
    ];

    public static readonly IReadOnlyList<UserRow> Users =
    [
        new("admin", "Admin", "Admin", true, "Pos, Stock, Catálogo, Fiscal, Usuarios"),
        new("caja1", "María López", "Cajero", true, "PosOperateRegister"),
        new("caja2", "Juan Pérez", "Cajero", true, "PosOperateRegister"),
        new("almacen", "Ana Ruiz", "Almacén", false, "StockAdjust, StockTransfer"),
    ];

    public static readonly IReadOnlyList<VeriFactuRow> VeriFactuRecords =
    [
        new("FS/2026-000124", "13/08/2026 13:56", 7.94m, "Aceptado (100)", "1000"),
        new("FS/2026-000123", "13/08/2026 13:42", 18.50m, "Aceptado (100)", "1000"),
        new("FS/2026-000122", "13/08/2026 12:30", 28.35m, "Aceptado (100)", "1000"),
        new("FS/2026-000121", "13/08/2026 12:45", 9.15m, "Pendiente", "—"),
        new("FS/2026-000120", "13/08/2026 11:32", 18.60m, "Rechazado (400)", "4001"),
    ];
}

public sealed record ProductRow(string Code, string Name, string Family, decimal Price, decimal Vat, int Stock, bool Active);
public sealed record CustomerRow(string Code, string Name, string Nif, string Phone, decimal Balance);
public sealed record TicketRow(string Number, string Date, string Time, decimal Total, string Employee, string Status);
public sealed record StockMovementRow(string When, string Type, string ProductCode, int Qty, int BalanceAfter);
public sealed record SupplierRow(string Code, string Name, string Nif, string Email, int OpenOrders);
public sealed record PurchaseOrderRow(string Number, string Supplier, string Date, string Status, decimal Total);
public sealed record UserRow(string Username, string DisplayName, string Role, bool Active, string Capabilities);
public sealed record VeriFactuRow(string InvoiceNo, string When, decimal Amount, string Status, string AeatCode);
public sealed record CartLineRow(string Code, string Name, decimal Qty, decimal Price, decimal DiscountPct, decimal LineTotal);
