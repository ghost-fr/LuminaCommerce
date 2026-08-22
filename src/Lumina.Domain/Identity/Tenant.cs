namespace Lumina.Domain.Identity;

/// <summary>
/// A tenant is the top-level owner boundary — one customer company using Lumina,
/// which may operate multiple Stores. Multi-tenancy is enforced at the repository
/// level: every query is implicitly scoped to the current TenantContext.
/// </summary>
public class Tenant
{
    public Guid Id { get; private set; }
    public string LegalName { get; private set; }
    public string Nif { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<Store> _stores = new();
    public IReadOnlyList<Store> Stores => _stores;

    private Tenant() { LegalName = string.Empty; Nif = string.Empty; }

    public Tenant(Guid id, string legalName, string nif)
    {
        if (string.IsNullOrWhiteSpace(legalName))
            throw new ArgumentException("Legal name is required.", nameof(legalName));
        if (string.IsNullOrWhiteSpace(nif))
            throw new ArgumentException("NIF is required for fiscal identification.", nameof(nif));

        Id = id;
        LegalName = legalName;
        Nif = nif;
        CreatedAt = DateTimeOffset.UtcNow;
        IsActive = true;
    }

    public Store AddStore(Guid id, string name, string sifBoundaryMode)
    {
        var store = new Store(id, Id, name, sifBoundaryMode);
        _stores.Add(store);
        return store;
    }
}

/// <summary>
/// A physical or logical point of sale location within a Tenant. StoreId is the
/// scoping unit most repository queries filter on once TenantId is established.
/// </summary>
public class Store
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string TimeZoneId { get; private set; }
    public bool IsActive { get; private set; }

    private Store() { Name = string.Empty; TimeZoneId = "Europe/Madrid"; }

    public Store(Guid id, Guid tenantId, string name, string sifBoundaryMode)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Store name is required.", nameof(name));

        Id = id;
        TenantId = tenantId;
        Name = name;
        TimeZoneId = "Europe/Madrid";
        IsActive = true;
    }
}
