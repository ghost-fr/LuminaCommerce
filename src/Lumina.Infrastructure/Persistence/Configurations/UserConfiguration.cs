using Lumina.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumina.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Username).IsRequired().HasMaxLength(100);
        b.Property(u => u.DisplayName).IsRequired().HasMaxLength(200);
        b.Property(u => u.PasswordHash).IsRequired().HasMaxLength(300);
        b.HasIndex(u => new { u.TenantId, u.Username }).IsUnique();

        // RoleIds stored as a comma-separated string for Phase 1 simplicity (SQLite,
        // no join table yet). Revisit as a proper many-to-many join entity if role
        // assignment ever needs its own metadata (assigned-by, assigned-at, etc).
        b.Property<string>("RoleIdsCsv")
            .HasColumnName("RoleIds")
            .HasMaxLength(2000);

        b.Metadata.FindNavigation(nameof(User.RoleIds))?.SetPropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.HasKey(r => r.Id);
        b.Property(r => r.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();

        // Capabilities stored as a comma-separated string for Phase 1 (SQLite has no
        // native array/set type). Swap for a proper value converter or join table
        // when moving to Postgres, which supports text[] natively.
        b.Property<string>("CapabilitiesCsv")
            .HasColumnName("Capabilities")
            .HasMaxLength(2000);
    }
}

/* Implementation note for whoever picks up the EF Core value-converter wiring:
 * User.RoleIds and Role.Capabilities are exposed as read-only collections on the
 * domain model (encapsulation per the blueprint's "rich domain objects, not
 * DataRows" principle). EF Core needs either:
 *   (a) backing-field access configured explicitly (started above), plus a
 *       ValueConverter<HashSet<string>, string> / List<Guid>, string> for the CSV
 *       columns, or
 *   (b) a shadow join table (cleaner, recommended) once this compiles and the first
 *       migration is generated.
 * Left as a Phase 1 follow-up rather than guessed at without a live EF Core tool to
 * verify the converter compiles — flagging explicitly instead of shipping something
 * that looks done but silently doesn't round-trip.
 */
