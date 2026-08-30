using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Honest GesVent module landing when the Lumina use-case is not built yet.
/// </summary>
public partial class ModulePlaceholderViewModel : ObservableObject
{
    public string ModuleName { get; }
    public string Title => ModuleName;
    public string GesVentKey { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Functions { get; }
    public bool IsLive { get; }

    public ModulePlaceholderViewModel(
        string moduleName,
        string gesVentKey,
        string summary,
        IReadOnlyList<string> functions,
        bool isLive = false)
    {
        ModuleName = moduleName;
        GesVentKey = gesVentKey;
        Summary = summary;
        Functions = functions;
        IsLive = isLive;
    }

    public static ModulePlaceholderViewModel Compras() => new(
        "Compras",
        "COM",
        "Pedidos, albaranes y facturas de proveedor. En GesVent: COMPED / COMALB / COMFAC. Lumina still needs the purchasing document state machine before this screen can write data.",
        [
            "Propuestas de pedido (COMPRO)",
            "Pedidos a proveedor (COMPED)",
            "Albaranes de compra (COMALB)",
            "Facturas de compra (COMFAC)",
            "Informes de compras (COMINF)",
        ]);

    public static ModulePlaceholderViewModel Ventas() => new(
        "Ventas",
        "VEN",
        "Documentos de venta mayor (no el ticket de caja). En GesVent: VENPED / VENALB / VENFAC. POS tickets already live under TPV.",
        [
            "Presupuestos (VENPRE)",
            "Pedidos de cliente (VENPED)",
            "Albaranes de venta (VENALB)",
            "Facturas de venta (VENFAC)",
            "Informes de ventas (VENINF)",
        ]);

    public static ModulePlaceholderViewModel Informes() => new(
        "Informes y gráficas",
        "INF",
        "Read-models: diario, IVA, formas de pago, visor de tickets. Needs reporting queries; do not copy GesVent DataSets into the UI.",
        [
            "Informe diario (INFTIE)",
            "Visor de tickets / cuadres (INFVTP)",
            "Ventas por artículo / familia / IVA",
            "Cuadrante de cajas (INFCCA)",
            "VeriFactu chain verification",
        ]);

    public static ModulePlaceholderViewModel Tablas() => new(
        "Tablas generales",
        "TAB",
        "Maestros: clientes, proveedores, tiendas, formas de pago, medidas. Catalogue products already have a live screen under Artículos.",
        [
            "Clientes (TABCLI)",
            "Proveedores (TABPRO)",
            "Tiendas (TABTIE)",
            "Formas de pago (TABFOR)",
            "Centros de stock (TABCEN)",
        ]);

    public static ModulePlaceholderViewModel Sistema() => new(
        "Sistema",
        "SIS",
        "Empresas, usuarios, configuración, backups, remoting. Today: login + capabilities. Config is appsettings / env, not CONFIG.XML.",
        [
            "Empresa activa (SISEMP / SISCEM)",
            "Usuarios y permisos (SISUSU)",
            "Configuración (SISCFG)",
            "Backups (SISBAK)",
            "Importar / exportar maestros y ventas",
        ]);
}
