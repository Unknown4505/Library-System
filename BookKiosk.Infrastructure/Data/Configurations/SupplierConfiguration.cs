using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(x => x.ContactName)
            .HasColumnType("nvarchar(100)")
            .HasMaxLength(100);

        builder.Property(x => x.PhoneNumber).HasColumnType("varchar(20)");
    }
}
