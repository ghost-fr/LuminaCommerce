using Lumina.Domain.Cash;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumina.Infrastructure.Persistence.Configurations;

public class RegisterSessionConfiguration : IEntityTypeConfiguration<RegisterSession>
{
    public void Configure(EntityTypeBuilder<RegisterSession> b)
    {
        b.ToTable("RegisterSessions");
        b.HasKey(s => s.Id);
        b.Property(s => s.OpeningFloat).HasColumnType("decimal(14,2)");
        b.Property(s => s.DeclaredClosingCash).HasColumnType("decimal(14,2)");
        b.Property(s => s.ExpectedClosingCash).HasColumnType("decimal(14,2)");
        b.Property(s => s.Discrepancy).HasColumnType("decimal(14,2)");
        // Hot-path query: "find the open session for this register" —
        // partial-index-like behavior isn't portable across SQLite/Postgres via
        // plain EF Core config, so this is a normal composite index; the
        // FindOpenByRegisterIdAsync query still filters Status = Open in SQL.
        b.HasIndex(s => new { s.RegisterId, s.Status });
    }
}

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> b)
    {
        b.ToTable("CashMovements");
        b.HasKey(m => m.Id);
        b.Property(m => m.Amount).HasColumnType("decimal(14,2)");
        b.Property(m => m.Reason).HasMaxLength(500);
        // Hot-path query: sum of movements for a session (register-close reconciliation).
        b.HasIndex(m => m.RegisterSessionId);
        // Append-only per the domain invariant (Domain/Cash/CashMovement.cs) —
        // no update/delete path exposed by ICashMovementRepository on purpose.
    }
}
