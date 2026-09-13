using System.Linq;
using Lumina.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        // RoleIds lives in private field _roleIds (List<Guid>). Persist as CSV.
        var roleIdsConverter = new ValueConverter<List<Guid>, string>(
            v => string.Join(",", v),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<Guid>()
                : v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Guid.Parse)
                    .ToList());

        var roleIdsComparer = new ValueComparer<List<Guid>>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v == null ? 0 : v.Aggregate(0, (h, g) => HashCode.Combine(h, g.GetHashCode())),
            v => v == null ? new List<Guid>() : v.ToList());

        b.Property<List<Guid>>("_roleIds")
            .HasField("_roleIds")
            .HasConversion(roleIdsConverter)
            .HasColumnName("RoleIds")
            .HasMaxLength(2000)
            .Metadata.SetValueComparer(roleIdsComparer);
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

        // Capabilities lives in private field _capabilities (HashSet<string>). Persist as CSV.
        var capsConverter = new ValueConverter<HashSet<string>, string>(
            v => string.Join(",", v),
            v => string.IsNullOrWhiteSpace(v)
                ? new HashSet<string>(StringComparer.Ordinal)
                : v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.Ordinal));

        var capsComparer = new ValueComparer<HashSet<string>>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SetEquals(b)),
            v => v == null ? 0 : v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode(StringComparison.Ordinal))),
            v => v == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(v, StringComparer.Ordinal));

        b.Property<HashSet<string>>("_capabilities")
            .HasField("_capabilities")
            .HasConversion(capsConverter)
            .HasColumnName("Capabilities")
            .HasMaxLength(2000)
            .Metadata.SetValueComparer(capsComparer);
    }
}
