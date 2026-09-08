using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumina.Infrastructure.Persistence.Configurations;

public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequenceRecord>
{
    public void Configure(EntityTypeBuilder<NumberSequenceRecord> b)
    {
        b.ToTable("NumberSequences");
        b.HasKey(s => new { s.StoreId, s.SequenceType });
        b.Property(s => s.SequenceType).HasMaxLength(30);
    }
}
