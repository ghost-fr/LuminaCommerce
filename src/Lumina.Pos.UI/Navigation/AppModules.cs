namespace Lumina.Pos.UI.Navigation;

/// <summary>
/// Structural spine from UI_STRUCTURE_BLUEPRINT.md §2 Hierarchy and §5 modules.
/// Do not rename/remove groups without updating the blueprint mapping.
/// </summary>
public static class AppModules
{
    public const string Pos = "pos";
    public const string SalesDocuments = "sales_documents";
    public const string Catalogue = "catalogue";
    public const string Pricing = "pricing";
    public const string Purchasing = "purchasing";
    public const string Inventory = "inventory";
    public const string Customers = "customers";
    public const string Reports = "reports";
    public const string Administration = "administration";
    public const string Integrations = "integrations";

    public static IReadOnlyList<NavGroup> BuildTree() => new[]
    {
        new NavGroup("POS / Sales tickets", new[]
        {
            new NavItem(Pos, "POS terminal", "Live checkout, cart, tender, ticket", "pos.operate_register"),
            new NavItem("pos.tickets", "Ticket viewer", "Inspect TPV tickets, payments, taxes", null),
            new NavItem("pos.registers", "Registers and cash", "Open/close register, withdrawals", null),
            new NavItem("pos.tables", "Tables and zones", "Floor plan / table setup", null),
        }),
        new NavGroup("Sales documents / Invoicing", new[]
        {
            new NavItem(SalesDocuments, "Documents", "Orders, delivery notes, invoices, budgets", null),
            new NavItem("sales.returns", "Returns and transfers", "Devolutions and document transfers", null),
            new NavItem("sales.receipts", "Receipts and remittances", "Collections and remittance groups", null),
        }),
        new NavGroup("Product catalogue", new[]
        {
            new NavItem(Catalogue, "Articles", "Search, edit, barcodes, properties", "admin.manage_catalogue"),
            new NavItem("cat.families", "Families / sections", "Hierarchy, VAT, units, labels", null),
        }),
        new NavGroup("Pricing and promotions", new[]
        {
            new NavItem(Pricing, "Price lists", "Sale/purchase tariffs", "admin.manage_pricing"),
            new NavItem("pricing.promos", "Promotions and discounts", "Offers, loyalty points", null),
        }),
        new NavGroup("Purchasing and suppliers", new[]
        {
            new NavItem(Purchasing, "Suppliers", "Master data, prices, offers", null),
            new NavItem("purch.orders", "Purchase documents", "Orders, delivery notes, invoices", null),
        }),
        new NavGroup("Inventory and logistics", new[]
        {
            new NavItem(Inventory, "Stock", "Levels, centers, reports", "stock.adjust"),
            new NavItem("inv.counts", "Inventory counts", "Counts, grouping, shrinkage", null),
            new NavItem("inv.transfers", "Warehouse transfers", "Store/warehouse movements", null),
        }),
        new NavGroup("Customers and master data", new[]
        {
            new NavItem(Customers, "Customers", "Master data, history, points", null),
            new NavItem("crm.geo", "Geography and banks", "Regions, payment methods", null),
        }),
        new NavGroup("Reports and analytics", new[]
        {
            new NavItem(Reports, "Report hub", "Parameters, preview, export", "admin.view_reports"),
        }),
        new NavGroup("Administration and security", new[]
        {
            new NavItem(Administration, "Users and permissions", "Roles, capabilities", "admin.manage_users"),
            new NavItem("admin.company", "Companies and stores", "Tenant, stores, terminals", null),
        }),
        new NavGroup("Integrations and maintenance", new[]
        {
            new NavItem(Integrations, "Backup and tools", "DB, devices, exchanges", null),
            new NavItem("int.fiscal", "VeriFactu / fiscal", "Hash chain, AEAT", "fiscal.manage_verifactu"),
        }),
    };
}

public sealed record NavGroup(string Title, IReadOnlyList<NavItem> Items);

public sealed record NavItem(
    string Id,
    string Title,
    string Description,
    string? RequiredCapability);
